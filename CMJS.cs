using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Beatmap.Base;
using Beatmap.Base.Customs;
using Beatmap.Containers;
using Beatmap.Enums;
using Beatmap.V2;
using Beatmap.V2.Customs;
using Beatmap.V3;
using Beatmap.V3.Customs;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[Plugin("CM JS")]
public class CMJS
{
    private NoteGridContainer notesContainer;
    private ChainGridContainer chainsContainer;
    private ArcGridContainer arcsContainer;
    private ObstacleGridContainer wallsContainer;
    private EventGridContainer eventsContainer;
    private CustomEventGridContainer customEventsContainer;
    private BPMChangeGridContainer bpmEventsContainer;
    private List<Check> checks = new List<Check>()
    {
        // Handle yourself...
    };
    private CheckResult errors;
    private UI ui;
    private AudioTimeSyncController atsc;
    private int index = 0;
    private bool movedAfterRun = false;

    private const string JsScriptDirectory = "CM-JsScripts";
    private const string DefaultJsScript = "cmjsupdate.js";

    [Init]
    private void Init()
    {
        // Standardsize directory creation for js scripts.
        EnsureJsScriptDirectoryExists();
        SceneManager.sceneLoaded += SceneLoaded;

        string assemblyFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        string jsPluginsFolder = Path.Combine(assemblyFolder, JsScriptDirectory);
        // depreciate load of JS scripts directly from plugins folder directly, 

        foreach (string file in Directory.GetFiles(assemblyFolder, "*.js"))
        {   // Yes we relocate them to new folder right before loading them
            string fileName = Path.GetFileName(file);
            // Construct the destination file path
            string destFilePath = Path.Combine(jsPluginsFolder, fileName);
            try
            {
                File.Move(file, destFilePath);
            }
            catch (IOException IOError) { Debug.LogError("IOException: thrown while trying to relocate script from: " + file + " To " + jsPluginsFolder + " <- this foler");
                Debug.LogError(IOError.Message);
            }
            continue; // Warn user about potential duplicate scripts so they  themselves can handle it
        }

        LoadPlugins(jsPluginsFolder);

        ui = new UI(this, checks);

        try
        {
            JintPatch.DoPatching();
        }
        catch (HarmonyException e)
        {
            Debug.LogError("Failed to patch Jint during CM-JS init");
            Debug.LogException(e);
            Debug.LogException(e.InnerException);
        }
    }
    private void EnsureJsScriptDirectoryExists()
    {
        // bruh this is kinda. DRY.... BADUM TSSSS.....
        string assemblyFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        string JsScriptDirectory = Path.Combine(assemblyFolder, "CM-JsScripts");
        if (!Directory.Exists(JsScriptDirectory))
        {
            Directory.CreateDirectory(JsScriptDirectory);
            // we will only create this js script if folder does not exist
            string defaultScriptPath = Path.Combine(JsScriptDirectory, DefaultJsScript);
            string defaultScriptContent = @"function performCheck(r) { return alert(""all your Chromapper JavaScripts are now all loaded/moved into: \n 'chromapper\\Plugins\\CM-JsScripts'. ;)""),null}module.exports={name:""CM-ScriptUpdateNotice"",params:{},run:performCheck};";

            File.WriteAllText(defaultScriptPath, defaultScriptContent);
        }
    }

    // TODO: allow an option to perhaps specify own folder for javascripts to be loaded from?
    private void LoadPlugins(string directory)
    {
        // loads javascripts from a new folder inside of plugins folder. AFTER  4 YEARS! Structure i guess
        foreach (string file in Directory.GetFiles(directory, "*.js"))
        {
            checks.Add(new ExternalJS(file));
        }
    }

    private void SceneLoaded(Scene arg0, LoadSceneMode arg1)
    {
        if (arg0.buildIndex == 3) // Mapper scene
        {
            notesContainer = UnityEngine.Object.FindObjectOfType<NoteGridContainer>();
            arcsContainer = UnityEngine.Object.FindObjectOfType<ArcGridContainer>();
            chainsContainer = UnityEngine.Object.FindObjectOfType<ChainGridContainer>();
            wallsContainer = UnityEngine.Object.FindObjectOfType<ObstacleGridContainer>();
            eventsContainer = UnityEngine.Object.FindObjectOfType<EventGridContainer>();
            customEventsContainer = UnityEngine.Object.FindObjectOfType<CustomEventGridContainer>();
            bpmEventsContainer = UnityEngine.Object.FindObjectOfType<BPMChangeGridContainer>();
            var mapEditorUI = UnityEngine.Object.FindObjectOfType<MapEditorUI>();

            atsc = BeatmapObjectContainerCollection.GetCollectionForType(ObjectType.Note).AudioTimeSyncController;

            // Add button to UI
            ui.AddButton(mapEditorUI);
        }
    }

    public void CheckErrors(Check check)
    {
        bool isV3 = Settings.Instance.Load_MapV3;


        if (errors != null)
        {
            // Remove error outline from old errors
            foreach (var block in errors.all)
            {
                if (BeatmapObjectContainerCollection.GetCollectionForType(ObjectType.Note).LoadedContainers.TryGetValue(block.note, out ObjectContainer container))
                {
                    container.OutlineVisible = SelectionController.IsObjectSelected(container.ObjectData);
                    container.SetOutlineColor(SelectionController.SelectedColor, false);
                }
            }
        }

        try
        {
            var vals = ui.paramTexts.Select((it, idx) =>
            {
                switch (it)
                {
                    case UITextInput textInput:
                        return new KeyValuePair<string, IParamValue>(check.Params[idx].name, check.Params[idx].Parse(textInput.InputField.text));
                    case UIDropdown dropdown:
                        return new KeyValuePair<string, IParamValue>(check.Params[idx].name, check.Params[idx].Parse(dropdown.Dropdown.value.ToString()));
                    case Toggle toggle:
                        return new KeyValuePair<string, IParamValue>(check.Params[idx].name, check.Params[idx].Parse(toggle.isOn.ToString()));
                    default:
                        return new KeyValuePair<string, IParamValue>(check.Params[idx].name, new ParamValue<string>(null)); // IDK
                }
            }).ToArray();

            if (isV3)
            {
                // TODO: since containers has multiple different object, check events and notes
                var allNotes = notesContainer.LoadedObjects.Where(it => it is V3ColorNote).Cast<BaseNote>().OrderBy(it => it.JsonTime).ToList();
                var allBombs = notesContainer.LoadedObjects.Where(it => it is V3BombNote).Cast<BaseNote>().OrderBy(it => it.JsonTime).ToList();
                var allArcs = arcsContainer.LoadedObjects.Cast<BaseArc>().OrderBy(it => it.JsonTime).ToList();
                var allChains = chainsContainer.LoadedObjects.Cast<BaseChain>().OrderBy(it => it.JsonTime).ToList();
                var allWalls = wallsContainer.LoadedObjects.Cast<BaseObstacle>().OrderBy(it => it.JsonTime).ToList();
                var allEvents = eventsContainer.LoadedObjects.Cast<BaseEvent>().OrderBy(it => it.JsonTime).ToList();
                var allCustomEvents = customEventsContainer.LoadedObjects.Cast<BaseCustomEvent>().OrderBy(it => it.JsonTime).ToList();
                var allBpmEvents = bpmEventsContainer.LoadedObjects.Cast<BaseBpmEvent>().OrderBy(it => it.JsonTime).ToList();
                errors = check.PerformCheck(allNotes, allBombs, allArcs, allChains, allEvents, allWalls, allCustomEvents, allBpmEvents, vals).Commit();
            } else
            {
                var allNotes = notesContainer.LoadedObjects.Cast<BaseNote>().OrderBy(it => it.JsonTime).ToList();
                var allWalls = wallsContainer.LoadedObjects.Cast<BaseObstacle>().OrderBy(it => it.JsonTime).ToList();
                var allEvents = eventsContainer.LoadedObjects.Cast<BaseEvent>().OrderBy(it => it.JsonTime).ToList();
                var allCustomEvents = customEventsContainer.LoadedObjects.Cast<BaseCustomEvent>().OrderBy(it => it.JsonTime).ToList();
                var allBpmEvents = bpmEventsContainer.LoadedObjects.Cast<BaseBpmEvent>().OrderBy(it => it.JsonTime).ToList();
                errors = check.PerformCheck(allNotes, new List<BaseNote>(), new List<BaseArc>(), new List<BaseChain>(), allEvents, allWalls, allCustomEvents, allBpmEvents, vals).Commit();
            }

            // Highlight blocks in loaded containers in case we don't scrub far enough with MoveToTimeInBeats to load them
            foreach (var block in errors.errors)
            {
                if (BeatmapObjectContainerCollection.GetCollectionForType(ObjectType.Note).LoadedContainers.TryGetValue(block.note, out ObjectContainer container))
                {
                    container.SetOutlineColor(Color.red);
                }
            }

            foreach (var block in errors.warnings)
            {
                if (BeatmapObjectContainerCollection.GetCollectionForType(ObjectType.Note).LoadedContainers.TryGetValue(block.note, out ObjectContainer container))
                {
                    container.SetOutlineColor(Color.yellow);
                }
            }

            index = 0;
            movedAfterRun = false;
            
            if (errors == null || errors.all.Count < 1)
            {
                ui.problemInfoText.text = "No problems found";
            }
            else
            {
                ui.problemInfoText.text = $"{errors.all.Count} problems found";
            }
            ui.problemInfoText.fontSize = 12;
            ui.problemInfoText.GetComponent<RectTransform>().sizeDelta = new Vector2(190, 50);
            //NextBlock(0);
        }
        catch (Exception e) { Debug.LogError(e.Message + e.StackTrace); }
    }

    public void NextBlock(int offset = 1)
    {
        if (!movedAfterRun)
        {
            movedAfterRun = true;
            if (offset > 0) offset = 0;
        }
        
        if (errors == null || errors.all.Count < 1)
        {
            return;
        }

        index = (index + offset) % errors.all.Count;

        if (index < 0)
        {
            index += errors.all.Count;
        }

        float? songBpmTime = errors.all[index]?.note.SongBpmTime;
        if (songBpmTime != null)
        {
            atsc.MoveToSongBpmTime(songBpmTime ?? 0);
        }

        if (ui.problemInfoText != null)
        {
            ui.problemInfoText.text = errors.all[index]?.reason ?? "...";
            ui.problemInfoText.fontSize = 12;
            ui.problemInfoText.GetComponent<RectTransform>().sizeDelta = new Vector2(190, 50);
        }
    }

    [ObjectLoaded]
    private void ObjectLoaded(ObjectContainer container)
    {
        if (container.ObjectData == null || errors == null) return;

        if (errors.errors.Any(it => it.note.Equals(container.ObjectData)))
        {
            container.SetOutlineColor(Color.red);
        }
        else if (errors.warnings.Any(it => it.note.Equals(container.ObjectData)))
        {
            container.SetOutlineColor(Color.yellow);
        }
    }

    [Exit]
    private void Exit()
    {
        
    }
}
