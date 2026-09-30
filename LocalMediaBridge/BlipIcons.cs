using System.Collections.Concurrent;

namespace MioCity.LocalMediaBridge;

/// <summary>
/// GTA blip icons for the companion map. The icons are not shipped with the app: the first time a sprite is needed it
/// is downloaded once from the FiveM documentation (docs.fivem.net/blips/radar_*.png), kept in
/// %LOCALAPPDATA%\MioCity\LocalMediaBridge\blips and served from this loopback server, so the map page can tint it
/// with the blip colour (a CSS mask needs a same-origin image) and never contacts another host itself.
/// </summary>
public static class BlipIcons
{
    private const int MaximumIconBytes = 64 * 1024;
    private static readonly Uri Source = new("https://docs.fivem.net/blips/");
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(6) };
    private static readonly ConcurrentDictionary<int, byte[]> Memory = new();
    private static readonly ConcurrentDictionary<int, DateTimeOffset> Failed = new();
    private static readonly ConcurrentDictionary<int, Lazy<Task<byte[]?>>> Pending = new();
    private static readonly string CacheDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MioCity", "LocalMediaBridge", "blips");

    // sprite id -> file name without the "radar_" prefix (from the FiveM blip reference)
    private static readonly Dictionary<int, string> Names = Parse(
        "1:level;4:wanted_radius;5:area_blip;6:centre;7:north;8:waypoint;9:radius_blip;10:radius_outline_blip;16:police" +
        "_plane_move;17:numbered_1;18:numbered_2;19:numbered_3;20:numbered_4;21:numbered_5;22:numbered_6;23:numbered_7;" +
        "24:numbered_8;25:numbered_9;26:numbered_10;27:mp_crew;28:mp_friendlies;29:empty;30:empty;31:empty;32:script_ob" +
        "jective;33:empty;34:empty;35:station;36:cable_car;37:activities;38:raceflag;39:fire;40:safehouse;43:police_hel" +
        "i;44:bomb_a;45:bomb_b;46:bomb_c;47:snitch;48:planning_locations;49:crim_arrest;50:crim_carsteal;51:crim_drugs;" +
        "52:crim_holdups;53:crim_pimping;54:crim_player;55:fence;56:cop_patrol;57:cop_player;58:crim_wanted;59:heist;60" +
        ":police_station;61:hospital;62:assassins_mark;63:elevator;64:helicopter;65:joyriders;66:random_character;67:se" +
        "curity_van;68:tow_truck;69:drive_thru;70:illegal_parking;71:barber;72:car_mod_shop;73:clothes_store;74:gym;75:" +
        "tattoo;76:armenian_family;77:lester_family;78:michael_family;79:trevor_family;80:jewelry_heist;81:drag_race;82" +
        ":drag_race_finish;83:car_carrier;84:rampage;85:vinewood_tours;86:lamar_family;87:taco_van;88:franklin_family;8" +
        "9:chinese_strand;90:flight_school;91:eye_sky;92:air_hockey;93:bar;94:base_jump;95:basketball;96:biolab_heist;9" +
        "7:bowling;98:burger_shot;99:cabaret_club;100:car_wash;101:cluckin_bell;102:comedy_club;103:darts;104:docks_hei" +
        "st;105:fbi_heist;106:fbi_officers_strand;107:finale_bank_heist;108:financier_strand;109:golf;110:gun_shop;111:" +
        "internet_cafe;112:michael_family_exile;113:nice_house_heist;114:random_female;115:random_male;116:repo;117:res" +
        "taurant;118:rural_bank_heist;119:shooting_range;120:solomon_strand;121:strip_club;122:tennis;123:trevor_family" +
        "_exile;124:michael_trevor_family;125:vehicle_spawn;126:triathlon;127:off_road_racing;128:gang_cops;129:gang_me" +
        "xicans;130:gang_bikers;131:gang_families;132:gang_professionals;133:snitch_red;134:crim_cuff_keys;135:cinema;1" +
        "36:music_venue;137:police_station_blue;138:airport;139:crim_saved_vehicle;140:weed_stash;141:hunting;142:pool;" +
        "143:objective_blue;144:objective_green;145:objective_red;146:objective_yellow;147:arms_dealing;148:mp_friend;1" +
        "49:celebrity_theft;150:weapon_assault_rifle;151:weapon_bat;152:weapon_grenade;153:weapon_health;154:weapon_kni" +
        "fe;155:weapon_molotov;156:weapon_pistol;157:weapon_rocket;158:weapon_shotgun;159:weapon_smg;160:weapon_sniper;" +
        "162:poi;163:passive;164:usingmenu;165:friend_franklin_p;166:friend_franklin_x;167:friend_michael_p;168:friend_" +
        "michael_x;169:friend_trevor_p;170:friend_trevor_x;171:gang_cops_partner;172:friend_lamar;173:weapon_minigun;17" +
        "5:weapon_armour;176:property_takeover;177:gang_mexicans_highlight;178:gang_bikers_highlight;179:triathlon_cycl" +
        "ing;180:triathlon_swimming;181:property_takeover_bikers;182:property_takeover_cops;183:property_takeover_vagos" +
        ";184:camera;185:centre_red;186:handcuff_keys_bikers;187:handcuff_keys_vagos;188:handcuffs_closed_bikers;189:ha" +
        "ndcuffs_closed_vagos;190:handcuffs_open_bikers;191:handcuffs_open_vagos;192:camera_badger;193:camera_facade;19" +
        "4:camera_ifruit;195:crim_arrest_bikers;196:crim_arrest_vagos;197:yoga;198:taxi;199:numbered_11;200:numbered_12" +
        ";201:numbered_13;202:numbered_14;203:numbered_15;204:numbered_16;205:shrink;206:epsilon;207:financier_strand_g" +
        "rey;208:trevor_family_grey;209:trevor_family_red;210:franklin_family_grey;211:franklin_family_blue;212:frankli" +
        "n_a;213:franklin_b;214:franklin_c;215:numbered_red_1;216:numbered_red_2;217:numbered_red_3;218:numbered_red_4;" +
        "219:numbered_red_5;220:numbered_red_6;221:numbered_red_7;222:numbered_red_8;223:numbered_red_9;224:numbered_re" +
        "d_10;225:gang_vehicle;226:gang_vehicle_bikers;227:gang_vehicle_cops;228:gang_vehicle_vagos;229:guncar;230:driv" +
        "ing_bikers;231:driving_cops;232:driving_vagos;233:gang_cops_highlight;234:shield_bikers;235:shield_cops;236:sh" +
        "ield_vagos;237:custody_bikers;238:custody_vagos;239:gang_wanted_bikers;240:gang_wanted_bikers_1;241:gang_wante" +
        "d_bikers_2;242:gang_wanted_bikers_3;243:gang_wanted_bikers_4;244:gang_wanted_bikers_5;245:gang_wanted_vagos;24" +
        "6:gang_wanted_vagos_1;247:gang_wanted_vagos_2;248:gang_wanted_vagos_3;249:gang_wanted_vagos_4;250:gang_wanted_" +
        "vagos_5;251:arms_dealing_air;252:playerstate_arrested;253:playerstate_custody;254:playerstate_driving;255:play" +
        "erstate_keyholder;256:playerstate_partner;257:gang_wanted_1;258:gang_wanted_2;259:gang_wanted_3;260:gang_wante" +
        "d_4;261:gang_wanted_5;262:ztype;263:stinger;264:packer;265:monroe;266:fairground;267:property;268:gang_highlig" +
        "ht;269:altruist;270:ai;271:on_mission;272:cash_pickup;273:chop;274:dead;275:territory_locked;276:cash_lost;277" +
        ":cash_vagos;278:cash_cops;279:hooker;280:friend;281:mission_2to4;282:mission_2to8;283:mission_2to12;284:missio" +
        "n_2to16;285:custody_dropoff;286:onmission_cops;287:onmission_lost;288:onmission_vagos;289:crim_carsteal_cops;2" +
        "90:crim_carsteal_bikers;291:crim_carsteal_vagos;292:band_strand;293:simeon_family;294:mission_1;295:mission_2;" +
        "296:friend_darts;297:friend_comedyclub;298:friend_cinema;299:friend_tennis;300:friend_stripclub;301:friend_liv" +
        "emusic;302:friend_golf;303:bounty_hit;304:ugc_mission;305:horde;306:cratedrop;307:plane_drop;308:sub;309:race;" +
        "310:deathmatch;311:arm_wrestling;312:mission_1to2;313:shootingrange_gunshop;314:race_air;315:race_land;316:rac" +
        "e_sea;317:tow;318:garbage;319:drill;320:spikes;321:firetruck;322:minigun2;323:bugstar;324:submarine;325:chinoo" +
        "k;326:getaway_car;327:mission_bikers_1;328:mission_bikers_1to2;329:mission_bikers_2;330:mission_bikers_2to4;33" +
        "1:mission_bikers_2to8;332:mission_bikers_2to12;333:mission_bikers_2to16;334:mission_cops_1;335:mission_cops_1t" +
        "o2;336:mission_cops_2;337:mission_cops_2to4;338:mission_cops_2to8;339:mission_cops_2to12;340:mission_cops_2to1" +
        "6;341:mission_vagos_1;342:mission_vagos_1to2;343:mission_vagos_2;344:mission_vagos_2to4;345:mission_vagos_2to8" +
        ";346:mission_vagos_2to12;347:mission_vagos_2to16;348:gang_bike;349:gas_grenade;350:property_for_sale;351:gang_" +
        "attack_package;352:martin_madrazzo;354:boost;355:devin;356:dock;357:garage;358:golf_flag;359:hangar;360:helipa" +
        "d;361:jerry_can;362:mask;363:heist_prep;364:incapacitated;365:spawn_point_pickup;366:boilersuit;367:completed;" +
        "368:rockets;369:garage_for_sale;370:helipad_for_sale;371:dock_for_sale;372:hangar_for_sale;373:placeholder_6;3" +
        "74:business;375:business_for_sale;376:race_bike;377:parachute;378:team_deathmatch;379:race_foot;380:vehicle_de" +
        "athmatch;381:barry;382:dom;383:maryann;384:cletus;385:josh;386:minute;387:omega;388:tonya;389:paparazzo;390:ai" +
        "m;391:cratedrop_background;392:green_and_net_player1;393:green_and_net_player2;394:green_and_net_player3;395:g" +
        "reen_and_friendly;396:net_player1_and_net_player2;397:net_player1_and_net_player3;398:creator;399:creator_dire" +
        "ction;400:abigail;401:blimp;402:repair;403:testosterone;404:dinghy;405:fanatic;406:invisible;407:info_icon;408" +
        ":capture_the_flag;409:last_team_standing;410:boat;411:capture_the_flag_base;412:mp_crew;413:capture_the_flag_o" +
        "utline;414:capture_the_flag_base_nobag;415:weapon_jerrycan;416:rp;417:level_inside;418:bounty_hit_inside;419:c" +
        "apture_the_usaflag;420:capture_the_usaflag_outline;421:tank;423:player_plane;424:player_jet;425:centre_stroke;" +
        "426:player_guncar;427:player_boat;428:mp_heist;429:temp_1;430:temp_2;431:temp_3;432:temp_4;433:temp_5;434:temp" +
        "_6;435:race_stunt;436:hot_property;437:urbanwarfare_versus;438:king_of_the_castle;439:player_king;440:dead_dro" +
        "p;441:penned_in;442:beast;443:edge_pointer;444:edge_crosstheline;445:mp_lamar;446:bennys;447:corner_number_1;4" +
        "48:corner_number_2;449:corner_number_3;450:corner_number_4;451:corner_number_5;452:corner_number_6;453:corner_" +
        "number_7;454:corner_number_8;455:yacht;456:finders_keepers;457:assault_package;458:hunt_the_boss;459:sightseer" +
        ";460:turreted_limo;461:belly_of_the_beast;462:yacht_location;463:pickup_beast;464:pickup_zoned;465:pickup_rand" +
        "om;466:pickup_slow_time;467:pickup_swap;468:pickup_thermal;469:pickup_weed;470:weapon_railgun;471:seashark;472" +
        ":pickup_hidden;473:warehouse;474:warehouse_for_sale;475:office;476:office_for_sale;477:truck;478:contraband;47" +
        "9:trailer;480:vip;481:cargobob;482:area_outline_blip;483:pickup_accelerator;484:pickup_ghost;485:pickup_detona" +
        "tor;486:pickup_bomb;487:pickup_armoured;488:stunt;489:weapon_lives;490:stunt_premium;491:adversary;492:biker_c" +
        "lubhouse;493:biker_caged_in;494:biker_turf_war;495:biker_joust;496:production_weed;497:production_crack;498:pr" +
        "oduction_fake_id;499:production_meth;500:production_money;501:package;502:capture_1;503:capture_2;504:capture_" +
        "3;505:capture_4;506:capture_5;507:capture_6;508:capture_7;509:capture_8;510:capture_9;511:capture_10;512:quad;" +
        "513:bus;514:drugs_package;515:pickup_jump;516:adversary_4;517:adversary_8;518:adversary_10;519:adversary_12;52" +
        "0:adversary_16;521:laptop;522:pickup_deadline;523:sports_car;524:warehouse_vehicle;525:reg_papers;526:police_s" +
        "tation_dropoff;527:junkyard;528:ex_vech_1;529:ex_vech_2;530:ex_vech_3;531:ex_vech_4;532:ex_vech_5;533:ex_vech_" +
        "6;534:ex_vech_7;535:target_a;536:target_b;537:target_c;538:target_d;539:target_e;540:target_f;541:target_g;542" +
        ":target_h;543:jugg;544:pickup_repair;545:steeringwheel;546:trophy;547:pickup_rocket_boost;548:pickup_homing_ro" +
        "cket;549:pickup_machinegun;550:pickup_parachute;551:pickup_time_5;552:pickup_time_10;553:pickup_time_15;554:pi" +
        "ckup_time_20;555:pickup_time_30;556:supplies;557:property_bunker;558:gr_wvm_1;559:gr_wvm_2;560:gr_wvm_3;561:gr" +
        "_wvm_4;562:gr_wvm_5;563:gr_wvm_6;564:gr_covert_ops;565:adversary_bunker;566:gr_moc_upgrade;567:gr_w_upgrade;56" +
        "8:sm_cargo;569:sm_hangar;570:tf_checkpoint;571:race_tf;572:sm_wp1;573:sm_wp2;574:sm_wp3;575:sm_wp4;576:sm_wp5;" +
        "577:sm_wp6;578:sm_wp7;579:sm_wp8;580:sm_wp9;581:sm_wp10;582:sm_wp11;583:sm_wp12;584:sm_wp13;585:sm_wp14;586:nh" +
        "p_bag;587:nhp_chest;588:nhp_orbit;589:nhp_veh1;590:nhp_base;591:nhp_overlay;592:nhp_turret;593:nhp_mg_firewall" +
        ";594:nhp_mg_node;595:nhp_wp1;596:nhp_wp2;597:nhp_wp3;598:nhp_wp4;599:nhp_wp5;600:nhp_wp6;601:nhp_wp7;602:nhp_w" +
        "p8;603:nhp_wp9;604:nhp_cctv;605:nhp_starterpack;606:nhp_turret_console;607:nhp_mg_mir_rotate;608:nhp_mg_mir_st" +
        "atic;609:nhp_mg_proxy;610:acsr_race_target;611:acsr_race_hotring;612:acsr_wp1;613:acsr_wp2;614:bat_club_proper" +
        "ty;615:bat_cargo;616:bat_truck;617:bat_hack_jewel;618:bat_hack_gold;619:bat_keypad;620:bat_hack_target;621:pic" +
        "kup_dtb_health;622:pickup_dtb_blast_increase;623:pickup_dtb_blast_decrease;624:pickup_dtb_bomb_increase;625:pi" +
        "ckup_dtb_bomb_decrease;626:bat_rival_club;627:bat_drone;628:bat_cash_reg;629:cctv;630:bat_assassinate;631:bat_" +
        "pbus;632:bat_wp1;633:bat_wp2;634:bat_wp3;635:bat_wp4;636:bat_wp5;637:bat_wp6;638:blimp_2;639:oppressor_2;640:b" +
        "at_wp7;641:arena_series;642:arena_premium;643:arena_workshop;644:race_wars;645:arena_turret;646:arena_rc_car;6" +
        "47:arena_rc_workshop;648:arena_trap_fire;649:arena_trap_flip;650:arena_trap_sea;651:arena_trap_turn;652:arena_" +
        "trap_pit;653:arena_trap_mine;654:arena_trap_bomb;655:arena_trap_wall;656:arena_trap_brd;657:arena_trap_sbrd;65" +
        "8:arena_bruiser;659:arena_brutus;660:arena_cerberus;661:arena_deathbike;662:arena_dominator;663:arena_impaler;" +
        "664:arena_imperator;665:arena_issi;666:arena_sasquatch;667:arena_scarab;668:arena_slamvan;669:arena_zr380;670:" +
        "ap;671:comic_store;672:cop_car;673:rc_time_trials;674:king_of_the_hill;675:king_of_the_hill_teams;676:rucksack" +
        ";677:shipping_container;678:agatha;679:casino;680:casino_table_games;681:casino_wheel;682:casino_concierge;683" +
        ":casino_chips;684:casino_horse_racing;685:adversary_featured;686:roulette_1;687:roulette_2;688:roulette_3;689:" +
        "roulette_4;690:roulette_5;691:roulette_6;692:roulette_7;693:roulette_8;694:roulette_9;695:roulette_10;696:roul" +
        "ette_11;697:roulette_12;698:roulette_13;699:roulette_14;700:roulette_15;701:roulette_16;702:roulette_17;703:ro" +
        "ulette_18;704:roulette_19;705:roulette_20;706:roulette_21;707:roulette_22;708:roulette_23;709:roulette_24;710:" +
        "roulette_25;711:roulette_26;712:roulette_27;713:roulette_28;714:roulette_29;715:roulette_30;716:roulette_31;71" +
        "7:roulette_32;718:roulette_33;719:roulette_34;720:roulette_35;721:roulette_36;722:roulette_0;723:roulette_00;7" +
        "24:limo;725:weapon_alien;726:race_open_wheel;727:rappel;728:swap_car;729:scuba_gear;730:cpanel_1;731:cpanel_2;" +
        "732:cpanel_3;733:cpanel_4;734:snow_truck;735:buggy_1;736:buggy_2;737:zhaba;738:gerald;739:ron;740:arcade;741:d" +
        "rone_controls;742:rc_tank;743:stairs;744:camera_2;745:winky;746:mini_sub;747:kart_retro;748:kart_modern;749:mi" +
        "litary_quad;750:military_truck;751:ship_wheel;752:ufo;753:seasparrow2;754:dinghy2;755:patrol_boat;756:retro_sp" +
        "orts_car;757:squadee;758:folding_wing_jet;759:valkyrie2;760:sub2;761:bolt_cutters;762:rappel_gear;763:keycard;" +
        "764:password;765:island_heist_prep;766:island_party;767:control_tower;768:underwater_gate;769:power_switch;770" +
        ":compound_gate;771:rappel_point;772:keypad;773:sub_controls;774:sub_periscope;775:sub_missile;776:painting;777" +
        ":car_meet;778:car_test_area;779:auto_shop_property;780:docks_export;781:prize_car;782:test_car;783:car_robbery" +
        "_board;784:car_robbery_prep;785:street_race_series;786:pursuit_series;787:car_meet_organiser;788:securoserv;78" +
        "9:bounty_collectibles;790:movie_collectibles;791:trailer_ramp;792:race_organiser;793:chalkboard_list;794:expor" +
        "t_vehicle;795:train;796:heist_diamond;797:heist_doomsday;798:heist_island;799:slamvan2;800:crusader;801:constr" +
        "uction_outfit;802:overlay_jammed;803:heist_island_unavailable;804:heist_diamond_unavailable;805:heist_doomsday" +
        "_unavailable;806:placeholder_7;807:placeholder_8;808:placeholder_9;809:featured_series;810:vehicle_for_sale;81" +
        "1:van_keys;812:suv_service;813:security_contract;814:safe;815:ped_r;816:ped_e;817:payphone;818:patriot3;819:mu" +
        "sic_studio;820:jubilee;821:granger2;822:explosive_charge;823:deity;824:d_champion;825:buffalo4;826:agency;827:" +
        "biker_bar;828:simeon_overlay;829:junk_skydive;830:luxury_car_showroom;831:car_showroom;832:car_showroom_simeon" +
        ";833:flaming_skull;834:weapon_ammo;835:community_series;836:cayo_series;837:clubhouse_contract;838:agent_ulp;8" +
        "39:acid;840:acid_lab;841:dax_overlay;842:dead_drop_package;843:downtown_cab;844:gun_van;845:stash_house;846:tr" +
        "actor;847:warehouse_juggalo;848:warehouse_juggalo_dax;849:weapon_crowbar;850:duffel_bag;851:oil_tanker;852:aci" +
        "d_lab_tent;853:van_burrito;854:acid_boost;855:ped_gang_leader;856:multistorey_garage;857:seized_asset_sales;85" +
        "8:cayo_attrition;859:bicycle;860:bicycle_trial;861:raiju;862:conada2;863:overlay_ready_for_sell;864:overlay_mi" +
        "ssing_supplies;865:streamer216;866:signal_jammer;867:salvage_yard;868:robbery_prep_equipment;869:robbery_prep_" +
        "overlay;870:yusuf;871:vincent;872:vinewood_garage;873:lstb;874:cctv_workstation;875:hacking_device;876:race_dr" +
        "ag;877:race_drift;878:casino_prep;879:planning_wall;880:weapon_crate;881:weapon_snowball;882:train_signals_gre" +
        "en;883:train_signals_red;884:office_transporter;885:yankton_survival;886:daily_bounty;887:bounty_target;888:fi" +
        "lming_schedule;889:pizza_this;890:aircraft_carrier;891:weapon_emp;892:maude_eccles;893:bail_bonds_office;894:w" +
        "eapon_emp_mine;895:zombie_disease;896:zombie_proximity;897:zombie_fire;898:animal_possessed;899:mobile_phone;9" +
        "00:garment_factory;901:garment_factory_for_sale;902:garment_factory_equipment;903:field_hangar;904:field_hanga" +
        "r_for_sale;905:cargobob_ch53;906:chopper_lift_ammo;907:chopper_lift_armor;908:chopper_lift_explosives;909:chop" +
        "per_lift_upgrade;910:chopper_lift_weapon;911:cargo_ship;912:submarine_missile;913:propeller_engine;914:shark;9" +
        "15:fast_travel;916:plane_duster2;917:plane_titan2;918:collectible;919:field_hangar_discount;920:garment_factor" +
        "y_discount;921:weapon_gusenberg_sweeper;922:weapon_tear_gas;923:dog;924:bobcat_security;925:smoke_shop;926:smo" +
        "ke_shop_for_sale;927:smoke_shop_attention;928:helitours;929:helitours_for_sale;930:helitours_attention;931:car" +
        "_wash_business;932:car_wash_business_for_sale;933:car_wash_business_attention;934:attention;935:alarm;936:heli" +
        "tours_discount;937:smoke_shop_discount;938:car_wash_business_discount;939:real_estate;940:medical_courier;941:" +
        "gruppe_sechs;942:fire_station;943:fire_truck;944:alpha_mail;945:ls_meteor;946:four20_survival;947:community_mi" +
        "ssion_series;948:property_mansion;949:ai_keypad;950:taxi_self_drive;951:train_subway;952:trashbag;953:mission_" +
        "creator;954:cat;955:mansion_ai_m;956:mansion_ai_f;957:mansion_ai_gang;958:heist_art_unavailable;959:heist_art;" +
        "960:property_mansion_art_heist;961:art_heist_prep;962:manhole_key;963:buyers_request;964:armored_caracara;965:" +
        "witness" +
        "");

    private static Dictionary<int, string> Parse(string packed)
    {
        var names = new Dictionary<int, string>();
        foreach (var entry in packed.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var split = entry.IndexOf(':');
            if (split > 0 && int.TryParse(entry.AsSpan(0, split), out var id)) names[id] = "radar_" + entry[(split + 1)..];
        }
        return names;
    }

    public static bool IsKnown(int sprite) => Names.ContainsKey(sprite);

    /// <summary>sprite id -> reference name (for the map's icon filter)</summary>
    public static IReadOnlyDictionary<int, string> AllNames => Names;

    public static async Task<byte[]?> GetAsync(int sprite, CancellationToken cancellationToken)
    {
        if (!Names.TryGetValue(sprite, out var name)) return null;
        if (Memory.TryGetValue(sprite, out var cached)) return cached;
        if (Failed.TryGetValue(sprite, out var failedAt) && DateTimeOffset.UtcNow - failedAt < TimeSpan.FromMinutes(10)) return null;
        // one download per sprite even when many map pages ask at once
        var work = Pending.GetOrAdd(sprite, _ => new Lazy<Task<byte[]?>>(() => LoadAsync(sprite, name)));
        try { return await work.Value.WaitAsync(cancellationToken); }
        finally { Pending.TryRemove(sprite, out _); }
    }

    private static async Task<byte[]?> LoadAsync(int sprite, string name)
    {
        var file = Path.Combine(CacheDirectory, name + ".png");
        try
        {
            if (File.Exists(file))
            {
                var local = await File.ReadAllBytesAsync(file);
                if (IsPng(local)) return Memory[sprite] = local;
            }
            using var response = await Http.GetAsync(new Uri(Source, name + ".png"), HttpCompletionOption.ResponseHeadersRead);
            if (!response.IsSuccessStatusCode || response.Content.Headers.ContentLength > MaximumIconBytes) throw new InvalidDataException();
            var bytes = await response.Content.ReadAsByteArrayAsync();
            if (bytes.Length > MaximumIconBytes || !IsPng(bytes)) throw new InvalidDataException();
            Directory.CreateDirectory(CacheDirectory);
            await File.WriteAllBytesAsync(file, bytes);
            Failed.TryRemove(sprite, out _);
            return Memory[sprite] = bytes;
        }
        catch (Exception)
        {
            Failed[sprite] = DateTimeOffset.UtcNow;
            return null;
        }
    }

    private static bool IsPng(byte[] bytes)
        => bytes.Length > 8 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4e && bytes[3] == 0x47;
}
