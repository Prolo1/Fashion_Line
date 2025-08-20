using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using UnityEngine;

using ProloAPI;
using ProloAPI.Extensions;

using KKAPI;
using KKAPI.Chara;
using KKAPI.Maker;
using KKAPI.Utilities;
using ExtensibleSaveFormat;
using KK_Plugins.MaterialEditor;
using KoiClothesOverlayX;
using Manager;
using ProloAPI.Utilities; 


//using BrowserFolders;

#if HONEY_API
using AIChara;
#else
using ChaCustom;
#endif

using static BepInEx.Logging.LogLevel;
//#if HONEY_API
//using MyBrowserFolders = BrowserFolders.AI_BrowserFolders;
//
//#elif KKS
//using MyBrowserFolders = BrowserFolders.KKS_BrowserFolders;
//#endif

namespace FashionLine
{
    using static FashionLine_Core;
    using static MaterialEditorCharaController;
    public class FashionLine_Controller : CharaCustomFunctionController
    {

        internal Dictionary<string, CoordData> fashionData = new Dictionary<string, CoordData>();
        public Dictionary<int, CoordData> defaultCoords { get; } = new Dictionary<int, CoordData>();
        private PluginData pluginData = null;
        private CoordData current = null;


        Coroutine co = null;

        int lastCoordType = -1;

        protected override void Awake()
        {
            base.Awake();

            OnCoordiniteTypeChangeEvent.AddListener(
                (a) =>
                {
#if KOI_API



                    if(ChaFileControl.status != a) return;


                    lastCoordType = ChaFileControl.status.coordinateType;

                    if(cfg.debug.Value)
                        Logger.LogDebug("Something changed I swear");

                    if(!defaultCoords.ContainsKey(lastCoordType))
                        defaultCoords[lastCoordType] = new CoordData().PopulateByFile(CreateTmpCoordFile(ChaControl.nowCoordinate));

#endif

                });
        }

        public void OnCharaReload(GameMode currentGameMode, bool keepState = false)
        {

            if(keepState) return;

            if(cfg.debug.Value)
                Logger.LogDebug("OnCharaReload called");

            //reset data
            {
                if(!cfg.areCoordinatesPersistant.Value)
                {
                    var line = fashionData.ToList();//copy list first
                    foreach(var fashion in line)
                        RemoveFashion(fashion.Key);
                    fashionData.Clear();
                }
                pluginData = null;
                defaultCoords.Clear();
            }

            //load new data
            pluginData = this.LoadExtData<CurrentSaveLoadManager, FashionLine_Controller>();

            //profit
        }


        void saveCoordExtDataTo(ChaFileCoordinate from, ChaFileCoordinate to)
        {

            if(cfg.debug.Value)
                Logger.LogDebug("Saving Coord Data");//old data
            var fromData = ExtendedSave.GetAllExtendedData(from);
            saveExtDataTo(fromData, to);
        }
        void saveCharaExtDataTo(ChaFile from, ChaFileCoordinate to)
        {
            if(cfg.debug.Value)
                Logger.LogDebug("Saving Character data");//old data
            var fromData = ExtendedSave.GetAllExtendedData(from);
            saveExtDataTo(fromData, to);
        }

        void saveExtDataTo(Dictionary<string, PluginData> fromData, ChaFileCoordinate to)
        {

            if(fromData != null)
                if(cfg.debug.Value)
                    Logger.LogDebug($"From Data:{(fromData?.Keys.Count > 0 ? "\n" : "")}" + string.Join("\n,", (fromData?.Keys.ToArray() ?? new string[] { })));//old data

            //if(fromData == null) return;

            var toData = ExtendedSave.GetAllExtendedData(to);
            if(toData == null)
                ExtendedSave.SetExtendedDataById(to, "some random string that no one will guess", new PluginData());

            toData = toData ?? ExtendedSave.GetAllExtendedData(to);


            if(fromData != null)
                foreach(var data in fromData)
                    ExtendedSave.SetExtendedDataById(to, data.Key, data.Value);

            toData = ExtendedSave.GetAllExtendedData(to);
            if(cfg.debug.Value)
                Logger.LogDebug("To Data:\n" + string.Join("\n,", toData?.Keys.ToArray() ?? new string[] { }) + "\n");//new data

            //toData[data.Key] = data.Value;
        }

        public void AddFashion(string name, CoordData data, bool overwrite = false)
        {
            if(data == null) return;

            try
            {
                if(!new ChaFileCoordinate().
                    LoadFile(new MemoryStream(data.data)
#if HONEY_API
                    , (int)Singleton<GameSystem>.Instance.language
#endif
                    ))
                    throw new Exception($"Was not able to read data from card [{name}] (Not a coordinate card)");


                if(!overwrite && fashionData.ContainsKey(name))
                    throw new Exception("This coordinate already exists (or one with the same name)");

                if(fashionData.ContainsKey(name))
                    FashionLine_GUI.RemoveCoordinate(fashionData[name]);

                FashionLine_GUI.AddCoordinate(in data);

                fashionData[name] = data;
            }
            catch(Exception e)
            {
                Logger.Log(Message | Error,
                    $"Could not add [{name}] to FashionLine:\n{e.Message}");
                Logger.Log(Error, $"\n{e.TargetSite} {e.StackTrace}\n");
            }
        }

        public void RemoveFashion(string name)
        {
            try
            {
                if(!fashionData.ContainsKey(name))
                    throw new Exception($"The name [{name}] does not exist in list");

                var tmp = fashionData[name];
                fashionData.Remove(name);

                if(!MakerAPI.InsideMaker) return;
                FashionLine_GUI.RemoveCoordinate(in tmp);
            }
            catch(Exception e)
            {
                Logger.Log(Message | Error,
                    $"Could not remove [{name}] from FashionLine:\n{e.Message}");
                Logger.Log(Error, $"{e.TargetSite}\n{e.StackTrace}\n");
            }
        }

        public void RemoveFashion(in CoordData data)
        {
            try
            {
                if(!fashionData.ContainsValue(data))
                    throw new Exception($"The CoordData [{data.name}] does not exist in list");

                var tmp = data;
                var name = fashionData.First((v) => v.Value == tmp).Key;
                fashionData.Remove(name);

                if(!MakerAPI.InsideMaker) return;
                FashionLine_GUI.RemoveCoordinate(in data);
            }

            catch(Exception e)
            {
                Logger.Log(Message | Error,
                    $"Could not remove [{data?.name ?? ""}] from FashionLine:\n{e.Message}");
                Logger.Log(Error, $"{e.TargetSite}\n{e.StackTrace}\n");
            }
        }

        public void NextInLine()
        {
            var line = fashionData.ToList();

            if(!fashionData.ContainsValue(current)) return;

            var index = line.FindIndex((l) => l.Value == current) + 1;
            index %= line.Count;

            if(line.InRange(index))
                WearFashion(line[index].Value, cfg.addToCurrentAccessories.Value);
        }

        public void PrevInLine()
        {
            var line = fashionData.ToList();

            if(!fashionData.ContainsValue(current)) return;
            var index = line.FindIndex((l) => l.Value == current) - 1;

            index = index < -1 ? 0 : index;
            index = index < 0 ? line.Count - 1 : index;

            if(line.InRange(index))
                WearFashion(line[index].Value, cfg.addToCurrentAccessories.Value);
        }

        public void WearFashion(in CoordData costume, bool combineAccessories, bool isFile = true, bool reload = true)
        {
            if(costume == null) return;

            current = costume;



            if(cfg.debug.Value)
                Logger.LogDebug("Wear fashion called");

            var coordinate = new ChaFileCoordinate();
            //ChaFileAccessory.PartsInfo[] accParts = null;
            //var ctrlKCO = GetComponent<KoiClothesOverlayController>();
            var ctrlMEC = GetComponent<MaterialEditorCharaController>();

            //var tmpLocation = (Directory.GetCurrentDirectory() + "/UserData/Tmp/FLine.png").MakeDirPath("/", "\\");
            try
            {

                //DummyChara<FashionLine_Controller>.Initialize = true;
                //
                //DummyChara<FashionLine_Controller>.extraCharacter.nowCoordinate = coordinate;
                //ctrlMEC = DummyChara<FashionLine_Controller>.extraCharacter.GetComponent<MaterialEditorCharaController>();

                // ctrlMEC.;
                if(isFile)
                {
                    using(MemoryStream stream = new MemoryStream(costume.data))
                    {
                        if(!coordinate.LoadFile(stream
#if HONEY_API
                        , (int)Singleton<GameSystem>.Instance.language
#endif
                        ))
                            Logger.Log(Warning | Message, $"Could not read card [{costume.name}]. Data size [{costume.data.Length}]");

                    }
                }
                else //is default caustume
                {
                    var tmp = (ChaFileCoordinate)costume.extras.Find((p) => p is ChaFileCoordinate);

                    if(tmp == null)
                        throw new NullReferenceException("Coordinate does not exist");


                    if(!coordinate.LoadFile(CreateTmpCoordFile(tmp)))
                        throw new ArgumentException("Could not load specified coordinate");



                }

                //remove excess
                var tmp1 = ChaControl.nowCoordinate.accessory.parts.ToList();
                var tmp2 = coordinate.accessory.parts.ToList();
                var last = 1 + tmp2.FindLastIndex(acc => acc.type > (int)ChaListDefine.CategoryNo.ao_none/*type is not none*/);
                tmp2.RemoveRange(last, tmp1.Count - last);
                // tmp1.RemoveAll(acc => acc.type <= (int)ChaListDefine.CategoryNo.ao_none/*none*/);
                // tmp2.RemoveAll(acc => acc.type <= (int)ChaListDefine.CategoryNo.ao_none/*none*/);

                // KK_Plugins.MaterialEditor.MaterialEditorCharaController

                var propLists = ctrlMEC.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
                       .Where(f => f.Name.Contains("List"));

                var newList = new List<object>();
                
                //copy over the accessories
                foreach(var prop in propLists)
                {
                    var list = prop.GetValue(GetComponent<MaterialEditorCharaController>());
                    var tmp = new List<object>();
                    {
#if KK

                        if(list is List<ProjectorProperty>)
                        {
                            foreach(var item in (List<ProjectorProperty>)list)
                                if(item.ObjectType == ObjectType.Accessory)
                                {
                                    ((ProjectorProperty)item).Slot += tmp1.Count;
                                    tmp.Add(item);
                                }
                            newList.Add(tmp);
                            ((List<ProjectorProperty>)prop.GetValue(ctrlMEC)).AddRange(tmp.Cast<ProjectorProperty>());
                        }
                        else
#endif
                        if(list is List<RendererProperty>)
                        {
                            // var newList = new List<RendererProperty>();
                            foreach(var item in (List<RendererProperty>)list)
                                if(item.ObjectType == ObjectType.Accessory)
                                {
                                    ((RendererProperty)item).Slot += tmp1.Count;
                                    tmp.Add(item);
                                }
                            newList.Add(tmp);
                            ((List<RendererProperty>)prop.GetValue(ctrlMEC)).AddRange(tmp.Cast<RendererProperty>());
                        }
                        else
                        if(list is List<MaterialFloatProperty>)
                        {
                            // var newList = new List<MaterialFloatProperty>();
                            foreach(var item in (List<MaterialFloatProperty>)list)
                                if(item.ObjectType == ObjectType.Accessory)
                                {
                                    ((MaterialFloatProperty)item).Slot += tmp1.Count;
                                    tmp.Add(item);
                                }
                            newList.Add(tmp);
                            ((List<MaterialFloatProperty>)prop.GetValue(ctrlMEC)).AddRange(tmp.Cast<MaterialFloatProperty>());
                        }
                        else
                        if(list is List<MaterialColorProperty>)
                        {
                            // var newList = new List<MaterialColorProperty>();
                            foreach(var item in (List<MaterialColorProperty>)list)
                                if(item.ObjectType == ObjectType.Accessory)
                                {
                                    ((MaterialColorProperty)item).Slot += tmp1.Count;
                                    tmp.Add(item);
                                }
                            newList.Add(tmp);
                            ((List<MaterialColorProperty>)prop.GetValue(ctrlMEC)).AddRange(tmp.Cast<MaterialColorProperty>());
                        }
                        else
                        if(list is List<MaterialKeywordProperty>)
                        {
                            // var newList = new List<MaterialKeywordProperty>();
                            foreach(var item in (List<MaterialKeywordProperty>)list)
                                if(item.ObjectType == ObjectType.Accessory)
                                {
                                    ((MaterialKeywordProperty)item).Slot += tmp1.Count;
                                    tmp.Add(item);
                                }
                            newList.Add(tmp);
                            ((List<MaterialKeywordProperty>)prop.GetValue(ctrlMEC)).AddRange(tmp.Cast<MaterialKeywordProperty>());
                        }
                        else
                        if(list is List<MaterialTextureProperty>)
                        {
                            // var newList = new List<MaterialTextureProperty>();
                            foreach(var item in (List<MaterialTextureProperty>)list)
                                if(item.ObjectType == ObjectType.Accessory)
                                {
                                    ((MaterialTextureProperty)item).Slot += tmp1.Count;
                                    tmp.Add(item);
                                }
                            newList.Add(tmp);
                            ((List<MaterialTextureProperty>)prop.GetValue(ctrlMEC)).AddRange(tmp.Cast<MaterialTextureProperty>());
                        }
                        else
                        if(list is List<MaterialShader>)
                        {
                            //  var newList = new List<MaterialShader>();
                            foreach(var item in (List<MaterialShader>)list)
                                if(item.ObjectType == ObjectType.Accessory)
                                {
                                    ((MaterialShader)item).Slot += tmp1.Count;
                                    tmp.Add(item);
                                }
                            newList.Add(tmp);
                            ((List<MaterialShader>)prop.GetValue(ctrlMEC)).AddRange(tmp.Cast<MaterialShader>());
                        }
                        else
                        if(list is List<MaterialCopy>)
                        {
                            //var newList = new List<MaterialCopy>();
                            foreach(var item in (List<MaterialCopy>)list)
                                if(item.ObjectType == ObjectType.Accessory)
                                {
                                    ((MaterialCopy)item).Slot += tmp1.Count;
                                    tmp.Add(item);
                                }
                            newList.Add(tmp);
                            ((List<MaterialCopy>)prop.GetValue(ctrlMEC)).AddRange(tmp.Cast<MaterialCopy>());
                        }
                        else
                        {
                            Logger.Log(Error | Message, $"List type not implemented: {list.GetType()}");
                        }
                    }


                }
                
                Logger.LogDebug("Saving coord data to tmp file");
                
                if(combineAccessories)
                    ctrlMEC.GetType().GetMethod("OnCoordinateBeingLoaded",
                          BindingFlags.NonPublic,
                        types: new Type[] { typeof(ChaControl), typeof(bool), },
                        binder: null, modifiers: null)
                        .Invoke(ctrlMEC, new object[]
                        { coordinate, false });
                
                Logger.LogDebug("Finisfed Saving coord data to tmp file");

                //add accessories to new coordinate
                foreach(var prop in propLists)
                {
                    var list = prop.GetValue(ctrlMEC);
#if KK
                    if(list is List<ProjectorProperty>)
                    {
                        foreach(var item in newList)
                            if(item is List<ProjectorProperty>)
                                prop.SetValue(ctrlMEC, ((List<ProjectorProperty>)list).AddNReturnRange((List<ProjectorProperty>)item));
                    }
                    else
#endif
                    if(list is List<RendererProperty>)
                    {
                        foreach(var item in newList)
                            if(item is List<RendererProperty>)
                                prop.SetValue(ctrlMEC, ((List<RendererProperty>)list).AddNReturnRange((List<RendererProperty>)item));
                    }
                    else
                    if(list is List<MaterialFloatProperty>)
                    {
                        foreach(var item in newList)
                            if(item is List<MaterialFloatProperty>)
                                prop.SetValue(ctrlMEC, ((List<MaterialFloatProperty>)list).AddNReturnRange((List<MaterialFloatProperty>)item));

                    }
                    else
                    if(list is List<MaterialColorProperty>)
                    {
                        foreach(var item in newList)
                            if(item is List<MaterialColorProperty>)
                                prop.SetValue(ctrlMEC, ((List<MaterialColorProperty>)list).AddNReturnRange((List<MaterialColorProperty>)item));

                    }
                    else
                    if(list is List<MaterialKeywordProperty>)
                    {
                        foreach(var item in newList)
                            if(item is List<MaterialKeywordProperty>)
                                prop.SetValue(ctrlMEC, ((List<MaterialKeywordProperty>)list).AddNReturnRange((List<MaterialKeywordProperty>)item));

                    }
                    else
                    if(list is List<MaterialTextureProperty>)
                    {
                        foreach(var item in newList)
                            if(item is List<MaterialTextureProperty>)
                                prop.SetValue(ctrlMEC, ((List<MaterialTextureProperty>)list).AddNReturnRange((List<MaterialTextureProperty>)item));

                    }
                    else
                    if(list is List<MaterialShader>)
                    {
                        foreach(var item in newList)
                            if(item is List<MaterialShader>)
                                prop.SetValue(ctrlMEC, ((List<MaterialShader>)list).AddNReturnRange((List<MaterialShader>)item));

                    }
                    else
                    if(list is List<MaterialCopy>)
                    {
                        foreach(var item in newList)
                            if(item is List<MaterialCopy>)
                                prop.SetValue(ctrlMEC, ((List<MaterialCopy>)list).AddNReturnRange((List<MaterialCopy>)item));

                    }
                    else
                    {
                        Logger.Log(Error | Message, $"List type not implemented: {list.GetType()}");
                    }
                }



                //combine access
                if(combineAccessories)
                {
                    coordinate.accessory.parts = tmp2.Concat(tmp1).ToArray();
                }
                // ChaControl.nowCoordinate.accessory.parts = new ChaFileAccessory.PartsInfo[0];
                // ChaControl.nowCoordinate.MemberInit();//reset coordinate


                //  if(isFile)
                ChaControl.nowCoordinate = coordinate;
                // else


                //save extended card data first
                if(combineAccessories)
                    typeof(CharacterApi).GetMethod("OnCoordinateBeingSaved",
                        BindingFlags.Static | BindingFlags.NonPublic,
                        types: new Type[] { typeof(ChaControl), typeof(ChaFileCoordinate), },
                        binder: null, modifiers: null)
                        .Invoke(null, new object[]
                        { ChaControl, coordinate });

                FashionReload(reload: reload);


                IEnumerator func(int delay)
                {
                    for(int a = 0; a < delay; ++a)
                        yield return null;

                    //  saveCoordDataTo(coordinate, ChaControl.nowCoordinate);//testing out removal
                    this.ForceInvokeCoordBeingLoaded(ChaControl.nowCoordinate);
                }

                StartCoroutine(func(0));

            }
            catch(Exception e)
            {
                Logger.Log(Error, $"Something went wrong: {e}\n");
                reload = false;
            }



        }

        public void WearDefaultFashion(bool reload = true)
        {
            if(cfg.debug.Value)
                Logger.LogDebug("wear default called");

            var costume = new CoordData() { name = "(default)" };
            var coord =
#if KOI_API
             (int)ChaControl.chaFile.status.coordinateType;
#else
                0;
#endif





            WearFashion(defaultCoords[coord], cfg.addToCurrentAccessories.Value, isFile: true, reload: reload);
        }

        private void FashionReload(bool reload = true, bool clothsOnly = true)
        {
            try
            {

                if(cfg.debug.Value)
                    Logger.LogDebug("Fashion reload called");

#if HONEY_API
                Singleton<Character>.Instance.customLoadGCClear = false;
#endif

                ChaControl.AssignCoordinate(
#if KOI_API
                (ChaFileDefine.CoordinateType)ChaControl.chaFile.status.coordinateType,
                //ChaFileDefine.CoordinateType.Plain
#endif
                ChaControl.nowCoordinate
                );

                if(reload)
                    ChaControl.Reload(false, clothsOnly, clothsOnly, clothsOnly
#if HONEY_API
                            , true
#endif
                    );
#if HONEY_API
                Singleton<Character>.Instance.customLoadGCClear = true;
#endif




                if(MakerAPI.InsideMaker)
                {

                    var mkBase = MakerAPI.GetMakerBase();

#if HONEY_API
                    mkBase.ChangeAcsSlotName(-1);
                    mkBase.forceUpdateAcsList = true;
#elif KOI_API
                    mkBase.updateCvsAccessoryChange =
                    mkBase.updateCvsAccessoryCopy = true;
#endif
                    mkBase.updateCustomUI = true;
                }
            }
            catch(Exception e) { Logger.LogError($"Reload did not complete:\n{e}"); }
        }

        private string CreateTmpCoordFile(ChaFileCoordinate data)
        {
            string tmpLocation = $"{(Directory.GetCurrentDirectory() + "/userdata/Tmp/FLine.png").MakeDirPath("/", "\\")}";

            //saveCoordExtDataTo(data, data);
            //data.pngData = UIGoku.EncodeToPNG();
            data?.SaveFile(Path.GetFileName(tmpLocation)
#if HONEY_API
                , (int)Singleton<GameSystem>.Instance.language
#endif
                );


            var lastSave = LastCoordSaveLocation;

            Directory.CreateDirectory(Path.GetDirectoryName(tmpLocation));

            if(File.Exists(lastSave))
            {
                if(File.Exists(tmpLocation))
                    File.Delete(tmpLocation);
                File.Move(lastSave, tmpLocation);
            }

            if(!File.Exists(tmpLocation))
                throw new FileNotFoundException($"Could not create tmp file: {tmpLocation}");


            return tmpLocation;
        }

        #region Helper Coroutines
        public IEnumerator AddFashionCo(uint delay, string name, CoordData data)
        {
            for(int a = 0; a < ((int)delay); ++a)
                yield return null;

            AddFashion(name, data);

            yield break;
        }

        public IEnumerator WearFashionCo(uint delay, CoordData costume, bool addAccessories, bool isFile = true, bool reload = true)
        {
            for(int a = 0; a < ((int)delay); ++a)
                yield return null;

            WearFashion(costume, addAccessories, isFile: isFile, reload: reload);

            yield break;
        }

        #endregion

        #region Class Overrides
        //protected override void Awake()
        //{
        //	base.Awake();
        //	//	KKAPI.Chara.CharacterApi.CharacterReloaded += PostAllLoad;
        //}

        //protected override void OnDestroy()
        //{
        //	//KKAPI.Chara.CharacterApi.CharacterReloaded -= PostAllLoad;
        //	base.OnDestroy();
        //}

        protected override void OnReload(GameMode currentGameMode, bool keepState)
        {

            //  if(keepState) return;
            OnCharaReload(currentGameMode, keepState);
        }

        protected override void OnCardBeingSaved(GameMode currentGameMode)
        {
            this.SaveExtData<CurrentSaveLoadManager, FashionLine_Controller>();
        }

        #endregion


    }

    public class CoordData
    {
        public byte[] data;
        public string name;
        public DateTime updated;
        public DateTime created;
        public string translatedName
        {
            get
            {
                TranslationHelper.TryTranslate(name, out var trans);
                return trans ?? name;
            }
        }



        public readonly List<object> extras = new List<object>();

        public CoordData()
        {
            updated = created = DateTime.Now;
        }

        public CoordData Clone()
        {
            var tmp = new CoordData()
            {
                data = data.ToArray(),
                name = name + "",
                created = new DateTime(created.Ticks),
                updated = DateTime.Now
            };
            tmp.extras.AddRange(extras);
            return tmp;
        }

        public bool Copy(CoordData dat)
        {
            if(dat == null) return false;

            var tmp = dat.Clone();
            extras.Clear();

            data = tmp.data;
            name = tmp.name;
            extras.AddRange(tmp.extras);

            return true;
        }


        public CoordData PopulateByFile(string path, bool clear = true)
        {
            path = path.MakeDirPath();

            var coord = new ChaFileCoordinate();
            coord.LoadFile(path);
            var name = Path.GetFileName(path);
            name = name.Substring(0, name.LastIndexOf('.'));
            name = !coord.coordinateName.IsNullOrWhiteSpace() ?
                coord.coordinateName ?? name : name;


            this.data = File.ReadAllBytes(path);
            this.name = name;
            this.created = File.GetCreationTime(path);
            this.updated = File.GetLastWriteTime(path);

            if(clear)
                extras.Clear();

            return this;
        }
    }
}