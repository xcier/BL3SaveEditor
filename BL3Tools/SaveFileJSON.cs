using BL3Tools.GameData.Items;
using BL3Tools.GVAS;

using Newtonsoft.Json;

using OakSave;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace BL3Tools
{
    internal class SaveFileJSON
    {

        public class SaveFileJSONFlaotConverter : JsonConverter<float>
        {
            public override float ReadJson(JsonReader reader, Type objectType,
                float existingValue, bool hasExistingValue, JsonSerializer serializer)
            {
                throw new NotImplementedException();
            }

            public override void WriteJson(JsonWriter writer,
                float value, JsonSerializer serializer)
            {

                writer.WriteRawValue(value.ToString());
            }
        }

        public BL3Save ToSaveFile(GVASSave saveData)
        {
            return new BL3Save(saveData, LoadCharacter());
        }


        private Character LoadCharacter()
        {
            try
            {
                var currentType = GetType().GetProperties();
                var instance = new Character();
                var props = typeof(Character).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(p => p.GetCustomAttribute<ProtoBuf.ProtoMemberAttribute>() != null)
                    .ToArray();
                foreach (var targetProp in props)
                {
                    ApplyProps(currentType, targetProp, instance, this);
                }
                return instance;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                //throw;
            }
            Console.WriteLine("Retry");
            return null;
        }

        public void PopulateCharacter(Character character)
        {
            try
            {
                var currentType = GetType().GetProperties();
                var props = typeof(Character).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(p => p.GetCustomAttribute<ProtoBuf.ProtoMemberAttribute>() != null)
                    .ToArray();
                foreach (var sourceProp in props)
                {
                    ApplyPropsFromCharacter(currentType, sourceProp, this, character);
                }
                foreach(var prop in GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    if(prop.GetValue(this) == null && prop.PropertyType.IsGenericType)
                    {
                        var genericList = typeof(List<>).MakeGenericType(prop.PropertyType.GetGenericArguments()[0]);
                        var listInstance = Activator.CreateInstance(genericList);
                        prop.SetValue(this, listInstance);
                    }
                }
                return;

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                //throw;
            }
            Console.WriteLine("Retry");
            return;
        }


        private void ApplyPropsFromCharacter(PropertyInfo[] targetProps, PropertyInfo sourceProp, object targetInstance, object sourceInstance)
        {
            if (sourceInstance == null)
                return;
            try
            {
                var targetPropName = sourceProp.GetCustomAttribute<ProtoBuf.ProtoMemberAttribute>().Name;

                var targetProp = targetProps.FirstOrDefault(p => p.Name == targetPropName);
                if (targetProp == null)
                    return;
                if (sourceProp.PropertyType.IsEnum)
                {
                    var val = (OakSave.MissionStatusPlayerSaveGameData.MissionState)sourceProp.GetValue(sourceInstance);
                    switch (val)
                    {
                        case OakSave.MissionStatusPlayerSaveGameData.MissionState.MSNotStarted:
                            targetProp.SetValue(targetInstance, "MS_NotStarted");
                            break;
                        case OakSave.MissionStatusPlayerSaveGameData.MissionState.MSActive:
                            targetProp.SetValue(targetInstance, "MS_Active");
                            break;
                        case OakSave.MissionStatusPlayerSaveGameData.MissionState.MSComplete:
                            targetProp.SetValue(targetInstance, "MS_Complete");
                            break;
                        case OakSave.MissionStatusPlayerSaveGameData.MissionState.MSFailed:
                            targetProp.SetValue(targetInstance, "MS_Failed");
                            break;
                        case OakSave.MissionStatusPlayerSaveGameData.MissionState.MSUnknown:
                            targetProp.SetValue(targetInstance, "MS_Unknown");
                            break;
                    }
                }
                if (sourceProp.PropertyType.IsValueType || sourceProp.PropertyType == typeof(string))
                {
                    if (sourceProp.PropertyType == typeof(string) || sourceProp.PropertyType == typeof(long))
                    {
                        targetProp.SetValue(targetInstance, sourceProp.GetValue(sourceInstance));
                    }
                    else if (sourceProp.PropertyType == typeof(float))
                    {
                        if (targetProp.PropertyType == typeof(long))
                            targetProp.SetValue(targetInstance, (long)(float)sourceProp.GetValue(sourceInstance));
                        else
                            targetProp.SetValue(targetInstance, sourceProp.GetValue(sourceInstance));
                    }
                    else if (sourceProp.PropertyType == typeof(int))
                    {
                        targetProp.SetValue(targetInstance, (long)(int)sourceProp.GetValue(sourceInstance));
                    }
                    else if (sourceProp.PropertyType == typeof(uint))
                    {
                        targetProp.SetValue(targetInstance, (long)(uint)sourceProp.GetValue(sourceInstance));
                    }
                    else if (sourceProp.PropertyType == typeof(ulong))
                    {
                        targetProp.SetValue(targetInstance, (long)(ulong)sourceProp.GetValue(sourceInstance));
                    }
                    else if (targetProp.PropertyType == typeof(bool))
                    {
                        targetProp.SetValue(targetInstance, sourceProp.GetValue(sourceInstance));
                    }
                }
                else if (sourceProp.PropertyType.IsArray)
                {
                    if (targetProp.PropertyType.IsGenericType && targetProp.PropertyType.GetGenericArguments()[0] == typeof(object))
                        return;
                    if (sourceProp.PropertyType.GetElementType() == typeof(byte) && targetProp.PropertyType == typeof(string))
                    {
                        targetProp.SetValue(targetInstance, Convert.ToBase64String(sourceProp.GetValue(sourceInstance) as byte[]));
                    }
                    else if (sourceProp.PropertyType.GetElementType().IsValueType || sourceProp.PropertyType.GetElementType() == typeof(string))
                    {
                        targetProp.SetValue(targetInstance, ToList(sourceProp.GetValue(sourceInstance)));
                    }
                    else
                    {
                        var newSourceProps = sourceProp.PropertyType.GetElementType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
                            .Where(p => p.GetCustomAttribute<ProtoBuf.ProtoMemberAttribute>() != null).ToArray();

                        var newTargetProps = targetProp.PropertyType.GetElementType().GetProperties(BindingFlags.Public | BindingFlags.Instance).ToArray();

                        var genericList = typeof(List<>).MakeGenericType(targetProp.PropertyType.GetElementType());
                        var listInstance = Activator.CreateInstance(genericList);
                        foreach (var newSourceInstance in sourceProp.GetValue(sourceInstance) as IEnumerable)
                        {
                            var newTargetInstance = Activator.CreateInstance(targetProp.PropertyType.GetElementType());
                            foreach (var newSourceProp in newSourceProps)
                            {
                                ApplyPropsFromCharacter(newTargetProps, newSourceProp, newTargetInstance, newSourceInstance);
                            }
                            AddToList(listInstance, newTargetInstance);
                        }
                        targetProp.SetValue(targetInstance, listInstance);

                    }
                }
                else if (sourceProp.PropertyType.IsGenericType && sourceProp.PropertyType.GetGenericTypeDefinition() == typeof(List<>))
                {
                    if (!targetProp.PropertyType.IsGenericType || targetProp.PropertyType.GetGenericArguments()[0] == typeof(object))
                        return;

                    //if (sourceProp.PropertyType.GetGenericArguments()[0] == typeof(byte) && targetProp.PropertyType.GetGenericArguments()[0] == typeof(string))
                    //{
                    //    targetProp.SetValue(targetInstance, Convert.FromBase64String(sourceProp.GetValue(sourceInstance) as string));
                    //}
                    if (sourceProp.PropertyType.GetGenericArguments()[0].IsValueType || sourceProp.PropertyType.GetGenericArguments()[0] == typeof(string))
                    {
                        targetProp.SetValue(targetInstance, sourceProp.GetValue(sourceInstance));
                    }
                    else
                    {
                        var newSourceProps = sourceProp.PropertyType.GetGenericArguments()[0].GetProperties(BindingFlags.Public | BindingFlags.Instance)
                            .Where(p => p.GetCustomAttribute<ProtoBuf.ProtoMemberAttribute>() != null).ToArray();

                        var newTargetProps = targetProp.PropertyType.GetGenericArguments()[0].GetProperties(BindingFlags.Public | BindingFlags.Instance).ToArray();

                        var genericList = typeof(List<>).MakeGenericType(targetProp.PropertyType.GetGenericArguments()[0]);
                        var listInstance = Activator.CreateInstance(genericList);
                        foreach (var newSourceInstance in sourceProp.GetValue(sourceInstance) as IEnumerable)
                        {
                            var newTargetInstance = Activator.CreateInstance(targetProp.PropertyType.GetGenericArguments()[0]);
                            foreach (var newSourceProp in newSourceProps)
                            {
                                ApplyPropsFromCharacter(newTargetProps, newSourceProp, newTargetInstance, newSourceInstance);
                            }
                            AddToList(listInstance, newTargetInstance);
                        }
                        targetProp.SetValue(targetInstance, listInstance);

                    }
                }
                else
                {
                    var newSourceProps = sourceProp.PropertyType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(p => p.GetCustomAttribute<ProtoBuf.ProtoMemberAttribute>() != null).ToArray();
                    var newTargetProps = targetProp.PropertyType.GetProperties(BindingFlags.Public | BindingFlags.Instance).ToArray();

                    var newTargetInstance = Activator.CreateInstance(targetProp.PropertyType);
                    foreach (var newSourceProp in newSourceProps)
                    {
                        ApplyPropsFromCharacter(newTargetProps, newSourceProp, newTargetInstance, sourceProp.GetValue(sourceInstance));
                    }
                    targetProp.SetValue(targetInstance, newTargetInstance);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        private void ApplyProps(PropertyInfo[] sourceProps, PropertyInfo targetProp, object targetInstance, object sourceInstance)
        {
            if (sourceInstance == null)
                return;
            try
            {
                var targetPropName = targetProp.GetCustomAttribute<ProtoBuf.ProtoMemberAttribute>().Name;
                if (targetPropName == "item_serial_number")
                    Console.WriteLine();
                    //Debugger.Break();
                var sourceProp = sourceProps.FirstOrDefault(p => p.Name == targetPropName);
                if (sourceProp == null)
                    return;
                if(targetProp.PropertyType.IsEnum)
                {
                    var val = sourceProp.GetValue(sourceInstance) as string;
                    switch (val)
                    {
                        case "MS_NotStarted":
                            targetProp.SetValue(targetInstance, OakSave.MissionStatusPlayerSaveGameData.MissionState.MSNotStarted);
                            break;
                        case "MS_Active":
                            targetProp.SetValue(targetInstance, OakSave.MissionStatusPlayerSaveGameData.MissionState.MSActive);
                            break;
                        case "MS_Complete":
                            targetProp.SetValue(targetInstance, OakSave.MissionStatusPlayerSaveGameData.MissionState.MSComplete);
                            break;
                        case "MS_Failed":
                            targetProp.SetValue(targetInstance, OakSave.MissionStatusPlayerSaveGameData.MissionState.MSFailed);
                            break;
                        case "MS_Unknown":
                            targetProp.SetValue(targetInstance, OakSave.MissionStatusPlayerSaveGameData.MissionState.MSUnknown);
                            break;
                    }
                }
                else if(targetProp.PropertyType.IsValueType || targetProp.PropertyType == typeof(string))
                {
                    if(targetProp.PropertyType == typeof(string) || targetProp.PropertyType == typeof(long))
                    {
                        targetProp.SetValue(targetInstance, sourceProp.GetValue(sourceInstance));
                    }
                    else if(targetProp.PropertyType == typeof(float))
                    {
                        if(sourceProp.PropertyType == typeof(long))
                            targetProp.SetValue(targetInstance, (float)(long)sourceProp.GetValue(sourceInstance));
                        else
                            targetProp.SetValue(targetInstance, sourceProp.GetValue(sourceInstance));
                    }
                    else if(targetProp.PropertyType == typeof(int))
                    {
                        targetProp.SetValue(targetInstance, (int)(long)sourceProp.GetValue(sourceInstance));
                    }
                    else if (targetProp.PropertyType == typeof(uint))
                    {
                        targetProp.SetValue(targetInstance, (uint)(long)sourceProp.GetValue(sourceInstance));
                    }
                    else if (targetProp.PropertyType == typeof(ulong))
                    {
                        targetProp.SetValue(targetInstance, (ulong)(long)sourceProp.GetValue(sourceInstance));
                    }
                    else if(targetProp.PropertyType == typeof(bool))
                    {
                        targetProp.SetValue(targetInstance, sourceProp.GetValue(sourceInstance));
                    }
                }
                else if(targetProp.PropertyType.IsArray) 
                {
                    if (sourceProp.PropertyType.IsGenericType && sourceProp.PropertyType.GetGenericArguments()[0] == typeof(object))
                        return;
                    if (targetProp.PropertyType.GetElementType() == typeof(byte) && sourceProp.PropertyType == typeof(string))
                    {
                        targetProp.SetValue(targetInstance, Convert.FromBase64String(sourceProp.GetValue(sourceInstance) as string));
                    }
                    else if(targetProp.PropertyType.GetElementType().IsValueType || targetProp.PropertyType.GetElementType() == typeof(string))
                    {
                        targetProp.SetValue(targetInstance, ToArray(sourceProp.GetValue(sourceInstance)));
                    }
                    else
                    {
                        var newTargetProps = targetProp.PropertyType.GetElementType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
                            .Where(p => p.GetCustomAttribute<ProtoBuf.ProtoMemberAttribute>() != null).ToArray();
                        var newSourceProps = sourceProp.PropertyType.GetElementType().GetProperties(BindingFlags.Public | BindingFlags.Instance).ToArray();
                        var genericList = typeof(List<>).MakeGenericType(targetProp.PropertyType.GetElementType());
                        var listInstance = Activator.CreateInstance(genericList);
                        foreach (var newSourceInstance in sourceProp.GetValue(sourceInstance) as IEnumerable)
                        {
                            var newTargetInstance = Activator.CreateInstance(targetProp.PropertyType.GetElementType());
                            foreach(var newTargetProp in newTargetProps)
                            {
                                ApplyProps(newSourceProps, newTargetProp, newTargetInstance, newSourceInstance);
                            }
                            AddToList(listInstance, newTargetInstance);
                        }
                        targetProp.SetValue(targetInstance, ToArray(listInstance));

                    }
                }
                else if (targetProp.PropertyType.IsGenericType &&  targetProp.PropertyType.GetGenericTypeDefinition() == typeof(List<>))
                {
                    if (!sourceProp.PropertyType.IsGenericType || sourceProp.PropertyType.GetGenericArguments()[0] == typeof(object))
                        return;

                    if (targetProp.PropertyType.GetGenericArguments()[0] == typeof(byte) && sourceProp.PropertyType.GetGenericArguments()[0] == typeof(string))
                    {
                        targetProp.SetValue(targetInstance, Convert.FromBase64String(sourceProp.GetValue(sourceInstance) as string));
                    }
                    else if (targetProp.PropertyType.GetGenericArguments()[0].IsValueType || targetProp.PropertyType.GetGenericArguments()[0] == typeof(string))
                    {
                        targetProp.SetValue(targetInstance, sourceProp.GetValue(sourceInstance));
                    }
                    else
                    {
                        var newTargetProps = targetProp.PropertyType.GetGenericArguments()[0].GetProperties(BindingFlags.Public | BindingFlags.Instance)
                            .Where(p => p.GetCustomAttribute<ProtoBuf.ProtoMemberAttribute>() != null).ToArray();
                        var newSourceProps = sourceProp.PropertyType.GetGenericArguments()[0].GetProperties(BindingFlags.Public | BindingFlags.Instance).ToArray();
                        var genericList = typeof(List<>).MakeGenericType(targetProp.PropertyType.GetGenericArguments()[0]);
                        var listInstance = Activator.CreateInstance(genericList);
                        foreach (var newSourceInstance in sourceProp.GetValue(sourceInstance) as IEnumerable)
                        {
                            var newTargetInstance = Activator.CreateInstance(targetProp.PropertyType.GetGenericArguments()[0]);
                            foreach (var newTargetProp in newTargetProps)
                            {
                                ApplyProps(newSourceProps, newTargetProp, newTargetInstance, newSourceInstance);
                            }
                            AddToList(listInstance, newTargetInstance);
                        }
                        targetProp.SetValue(targetInstance, listInstance);

                    }
                }
                else
                {
                    var newTargetProps = targetProp.PropertyType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(p => p.GetCustomAttribute<ProtoBuf.ProtoMemberAttribute>() != null).ToArray();
                    var newSourceProps = sourceProp.PropertyType.GetProperties(BindingFlags.Public | BindingFlags.Instance).ToArray();

                    var newTargetInstance = Activator.CreateInstance(targetProp.PropertyType);
                    foreach (var newTargetProp in newTargetProps)
                    {
                        ApplyProps(newSourceProps, newTargetProp, newTargetInstance, sourceProp.GetValue(sourceInstance));
                    }
                    targetProp.SetValue(targetInstance, newTargetInstance);
                }
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }



        private int GetArrayLength(object array)
        {
            return (int)array.GetType().GetProperty("Length", BindingFlags.Instance | BindingFlags.Public).GetValue(array);
        }

        private void AddToList(object list, object item)
        {
            list.GetType().GetMethod("Add", BindingFlags.Public | BindingFlags.Instance).Invoke(list, new[] { item });
        }

        private object ToArray(object list)
        {
            return typeof(System.Linq.Enumerable).GetMethod("ToArray").MakeGenericMethod(list.GetType().GetGenericArguments()[0]).Invoke(null, new[] { list });
        }

        private object ToList(object array)
        {
            return typeof(System.Linq.Enumerable).GetMethod("ToList").MakeGenericMethod(array.GetType().GetElementType()).Invoke(null, new[] { array });
        }

        public Last_Traveled_Map_Id last_traveled_map_id { get; set; }
        /// 
        /// 
        /// 
        public long save_game_id { get; set; }
        public long last_save_timestamp { get; set; }
        public long time_played_seconds { get; set; }
        public Player_Class_Data player_class_data { get; set; }
        public List<Resource_Pools> resource_pools { get; set; }
        public List<Saved_Regions> saved_regions { get; set; }
        public long experience_points { get; set; }
        public List<Game_Stats_Data> game_stats_data { get; set; }
        public List<Inventory_Category_List> inventory_category_list { get; set; }
        public List<Inventory_Items> inventory_items { get; set; }
        public List<Equipped_Inventory_List> equipped_inventory_list { get; set; }
        public List<int> active_weapon_list { get; set; }
        public Ability_Data ability_data { get; set; }
        public long last_play_through_index { get; set; }
        public long playthroughs_completed { get; set; }
        public bool show_new_playthrough_notification { get; set; }
        public List<Mission_Playthroughs_Data> mission_playthroughs_data { get; set; }
        public List<string> active_travel_stations { get; set; }
        public Discovery_Data discovery_data { get; set; }
        public string last_active_travel_station { get; set; }
        public List<Vehicles_Unlocked_Data> vehicles_unlocked_data { get; set; }
        public List<string> vehicle_parts_unlocked { get; set; }
        public List<Vehicle_Loadouts> vehicle_loadouts { get; set; }
        public long vehicle_last_loadout_index { get; set; }
        public List<Challenge_Data> challenge_data { get; set; }
        public List<Sdu_List> sdu_list { get; set; }
        public List<string> selected_customizations { get; set; }
        public List<int> equipped_emote_customizations { get; set; }
        public List<Selected_Color_Customizations> selected_color_customizations { get; set; }
        public Guardian_Rank guardian_rank { get; set; }
        public Crew_Quarters_Room crew_quarters_room { get; set; }
        public Crew_Quarters_Gun_Rack crew_quarters_gun_rack { get; set; }
        public List<Unlocked_Echo_Logs> unlocked_echo_logs { get; set; }
        public bool has_played_special_echo_log_insert_already { get; set; }

        [JsonConverter(typeof(ObjectToArrayConverter<Nickname_Mappings, string, string>))]
        public List<Nickname_Mappings> nickname_mappings { get; set; }
        public Challenge_Category_Completion_Pcts challenge_category_completion_pcts { get; set; }
        public Character_Slot_Save_Game_Data character_slot_save_game_data { get; set; }
        public Ui_Tracking_Save_Game_Data ui_tracking_save_game_data { get; set; }
        public string preferred_character_name { get; set; }
        public long name_character_limit { get; set; }
        public long preferred_group_mode { get; set; }
        public Time_Of_Day_Save_Game_Data time_of_day_save_game_data { get; set; }
        public List<Level_Persistence_Data> level_persistence_data { get; set; }
        public long accumulated_level_persistence_reset_timer_seconds { get; set; }
        public long mayhem_level { get; set; }
        public Gbx_Zone_Map_Fod_Save_Game_Data gbx_zone_map_fod_save_game_data { get; set; }
        public List<object> active_or_blacklisted_travel_stations { get; set; }
        public List<string> last_active_travel_station_for_playthrough { get; set; }
        public List<Game_State_Save_Data_For_Playthrough> game_state_save_data_for_playthrough { get; set; }
        public List<Registered_Downloadable_Entitlements> registered_downloadable_entitlements { get; set; }
        public List<Active_Travel_Stations_For_Playthrough> active_travel_stations_for_playthrough { get; set; }
        public string save_game_guid { get; set; }
        public Guardian_Rank_Character_Data guardian_rank_character_data { get; set; }
        public bool optional_objective_reward_fixup_applied { get; set; }
        public bool vehicle_part_rewards_fixup_applied { get; set; }
        public long last_active_league { get; set; }
        public long last_active_league_instance { get; set; }
        
        [JsonConverter(typeof(ObjectToArrayConverter<Active_League_Instance_For_Event, long, long>))]
        public List<Active_League_Instance_For_Event> active_league_instance_for_event { get; set; }
        public bool levelled_save_vehicle_part_rewards_fixup_applied { get; set; }

        public class Player_Class_Data
        {
            public string player_class_path { get; set; }
            public long dlc_package_id { get; set; }
        }

        public class Ability_Data
        {
            public long ability_points { get; set; }
            public List<Tree_Item_List> tree_item_list { get; set; }
            public List<object> ability_slot_list { get; set; }
            public List<object> augment_slot_list { get; set; }
            public List<object> augment_configuration_list { get; set; }
            public long tree_grade { get; set; }
        }

        public class Tree_Item_List
        {
            public string item_asset_path { get; set; }
            public long points { get; set; }
            public long max_points { get; set; }
            public long tree_identifier { get; set; }
        }

        public class Discovery_Data
        {
            public List<Discovered_Level_Info> discovered_level_info { get; set; }
        }

        public class Discovered_Level_Info
        {
            public string discovered_level_name { get; set; }
            public long discovered_playthroughs { get; set; }
            public List<Discovered_Area_Info> discovered_area_info { get; set; }
        }

        public class Discovered_Area_Info
        {
            public string discovered_area_name { get; set; }
            public long discovered_playthroughs { get; set; }
        }

        public class Guardian_Rank
        {
            public long guardian_rank { get; set; }
            public long guardian_experience { get; set; }
        }

        public class Crew_Quarters_Room
        {
            public long preferred_room_assignment { get; set; }
            public List<Decoration> decorations { get; set; }
            public string room_data_path { get; set; }
        }

        public class Decoration
        {
            public long decoration_index { get; set; }
            public string decoration_data_path { get; set; }
        }

        public class Crew_Quarters_Gun_Rack
        {
            public List<object> rack_save_data { get; set; }
        }

        public class Nickname_Mappings : IKeyValueJSON<string, string>
        {
            public string key { get; set; }
            public string value { get; set; }
        }

        public class Challenge_Category_Completion_Pcts
        {
            public string category_progress { get; set; }
        }

        public class Character_Slot_Save_Game_Data
        {
            public List<object> augment_slot_list { get; set; }
        }

        public class Ui_Tracking_Save_Game_Data
        {
            public bool has_seen_skill_menu_unlock { get; set; }
            public bool has_seen_guardian_rank_menu_unlock { get; set; }
            public bool has_seen_echo_boot_ammo_bar { get; set; }
            public bool has_seen_echo_boot_shield_bar { get; set; }
            public bool has_seen_echo_boot_grenades { get; set; }
            public long highest_thvm_breadcrumb_seen { get; set; }
            public List<string> inventory_slot_unlocks_seen { get; set; }
            public long saved_spin_offset { get; set; }
        }

        public class Time_Of_Day_Save_Game_Data
        {
            public List<Planet_Cycle_Info> planet_cycle_info { get; set; }
            public string planet_cycle { get; set; }
        }

        public class Planet_Cycle_Info
        {
            public string planet_name { get; set; }
            public float cycle_length { get; set; }
            public float last_cached_time { get; set; }
        }

        public class Gbx_Zone_Map_Fod_Save_Game_Data
        {
            public List<Level_Data> level_data { get; set; }
        }

        public class Level_Data
        {
            public string level_name { get; set; }
            public long fod_texture_size { get; set; }
            public long num_chunks { get; set; }
            public float discovery_percentage { get; set; }
            public long data_state { get; set; }
            public long data_revision { get; set; }
            public string fod_data { get; set; }
        }

        public class Guardian_Rank_Character_Data
        {
            public long guardian_available_tokens { get; set; }
            public long guardian_rank { get; set; }
            public long guardian_experience { get; set; }
            public List<Rank_Rewards> rank_rewards { get; set; }
            public List<Rank_Perks> rank_perks { get; set; }
            public long guardian_reward_random_seed { get; set; }
            public long new_guardian_experience { get; set; }
            public bool is_rank_system_enabled { get; set; }
        }

        public class Rank_Rewards
        {
            public long num_tokens { get; set; }
            public bool is_enabled { get; set; }
            public string reward_data_path { get; set; }
        }

        public class Rank_Perks
        {
            public bool is_enabled { get; set; }
            public string perk_data_path { get; set; }
        }

        public class Active_League_Instance_For_Event : IKeyValueJSON<long, long>
        {
            public long key { get; set; }
            public long value { get; set; }

        }

        public class Resource_Pools
        {
            public long amount { get; set; }
            public string resource_path { get; set; }
        }

        public class Saved_Regions
        {
            public long game_stage { get; set; }
            public long play_through_idx { get; set; }
            public string region_path { get; set; }
            public long dlc_package_id { get; set; }
        }

        public class Game_Stats_Data
        {
            public long stat_value { get; set; }
            public string stat_path { get; set; }
        }

        public class Inventory_Category_List
        {
            public long base_category_definition_hash { get; set; }
            public long quantity { get; set; }
        }

        public class Inventory_Items
        {
            public string item_serial_number { get; set; }
            public long pickup_order_index { get; set; }
            public long flags { get; set; }
            public string weapon_skin_path { get; set; }
        }

        public class Equipped_Inventory_List
        {
            public long inventory_list_index { get; set; }
            public bool enabled { get; set; }
            public string slot_data_path { get; set; }
            public string trinket_data_path { get; set; }
        }

        public class Mission_Playthroughs_Data
        {
            public List<Mission_List> mission_list { get; set; }
            public string tracked_mission_class_path { get; set; }
        }

        public class Mission_List
        {
            public string status { get; set; }
            public bool has_been_viewed_in_log { get; set; }
            public List<int> objectives_progress { get; set; }
            public string mission_class_path { get; set; }
            public string active_objective_set_path { get; set; }
            public long dlc_package_id { get; set; }
            public bool kickoff_played { get; set; }
            public long league_instance { get; set; }
        }

        public class Vehicles_Unlocked_Data
        {
            public string asset_path { get; set; }
            public bool just_unlocked { get; set; }
        }

        public class Vehicle_Loadouts
        {
            public string loadout_save_name { get; set; }
            public string body_asset_path { get; set; }
            public string wheel_asset_path { get; set; }
            public string armor_asset_path { get; set; }
            public string core_mod_asset_path { get; set; }
            public string gunner_weapon_asset_path { get; set; }
            public string driver_weapon_asset_path { get; set; }
            public string ornament_asset_path { get; set; }
            public string material_decal_asset_path { get; set; }
            public string material_asset_path { get; set; }
            public long color_index_1 { get; set; }
            public long color_index_2 { get; set; }
            public long color_index_3 { get; set; }
        }

        public class Challenge_Data
        {
            public long completed_count { get; set; }
            public bool is_active { get; set; }
            public bool currently_completed { get; set; }
            public long completed_progress_level { get; set; }
            public long progress_counter { get; set; }
            public List<Stat_Instance_State> stat_instance_state { get; set; }
            public string challenge_class_path { get; set; }
            public List<Challenge_Reward_Info> challenge_reward_info { get; set; }
        }

        public class Stat_Instance_State
        {
            public long current_stat_value { get; set; }
            public string challenge_stat_path { get; set; }
        }

        public class Challenge_Reward_Info
        {
            public bool challenge_reward_claimed { get; set; }
        }

        public class Sdu_List
        {
            public long sdu_level { get; set; }
            public string sdu_data_path { get; set; }
        }

        public class Selected_Color_Customizations
        {
            public string color_parameter { get; set; }
            public Applied_Color applied_color { get; set; }
            public Split_Color split_color { get; set; }
            public bool use_default_color { get; set; }
            public bool use_default_split_color { get; set; }
        }

        public class Applied_Color
        {
            public float x { get; set; }
            public float y { get; set; }
            public float z { get; set; }
        }

        public class Split_Color
        {
            public float x { get; set; }
            public float y { get; set; }
            public float z { get; set; }
        }

        public class Unlocked_Echo_Logs
        {
            public bool has_been_seen_in_log { get; set; }
            public string echo_log_path { get; set; }
        }

        public class Level_Persistence_Data
        {
            public string level_name { get; set; }
            public List<Saved_Actors> saved_actors { get; set; }
        }

        public class Saved_Actors
        {
            public string actor_name { get; set; }
            public long timer_remaining { get; set; }
        }

        public class Game_State_Save_Data_For_Playthrough
        {
            public Last_Traveled_Map_Id last_traveled_map_id { get; set; }
            public long mayhem_level { get; set; }
            public long mayhem_random_seed { get; set; }
        }

        public class Last_Traveled_Map_Id
        {
            public long zone_name_id { get; set; }
            public long map_name_id { get; set; }
        }

        public class Registered_Downloadable_Entitlements
        {
            public string entitlement_source_asset_path { get; set; }
            public List<object> entitlement_ids { get; set; }
            public List<Entitlement> entitlements { get; set; }
        }

        public class Entitlement
        {
            public long id { get; set; }
            public long consumed { get; set; }
            public bool registered { get; set; }
            public bool seen { get; set; }
        }

        public class Active_Travel_Stations_For_Playthrough
        {
            public List<Active_Travel_Stations> active_travel_stations { get; set; }
        }

        public class Active_Travel_Stations
        {
            public string active_travel_station_name { get; set; }
            public bool blacklisted { get; set; }
        }

    }
}
