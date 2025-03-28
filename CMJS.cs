﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Beatmap.Base;
using Beatmap.Containers;
using Beatmap.Enums;
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

    protected const string JsScriptDirectory = "CM-JS-Scripts";
    protected const string DefaultJsScript = "cmjsupdate.js";
    private readonly string jsPluginsFolder = GetJsScriptFolder();

    [Init]
    private void Init()
    {
        // Standardize directory creation for java Scripts.
        EnsureJsScriptDirectoryExists();
        SceneManager.sceneLoaded += SceneLoaded;

        string assemblyFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        
        // deprecate load of JS scripts directly from plugins folder directly, 
        foreach (string file in Directory.GetFiles(assemblyFolder, "*.js"))
        {   // Yes we relocate them to new folder right before loading them
            string fileName = Path.GetFileName(file);
            // Construct the destination file path
            string destFilePath = Path.Combine(jsPluginsFolder, fileName);
            try
            {
                File.Move(file, destFilePath);
            }
            catch (IOException IOError) { Debug.LogError("IOException: thrown while trying to relocate script from: " + file + " to " + jsPluginsFolder + " <- this folder");
                Debug.LogError(IOError.Message);
            }
            continue; // Warn user about potential duplicate scripts so they  themselves can handle it
        }
        LoadJavaScripts(jsPluginsFolder);
      
    }
    // Litterly in the name
    public static string GetJsScriptFolder()
    {
        string assemblyFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        return Path.Combine(assemblyFolder, JsScriptDirectory);
    }

    private void EnsureJsScriptDirectoryExists()
    {
        // Cleaner handling when we want the path for JsScriptDirectory
        string JsScriptDirectory = GetJsScriptFolder();
        if (!Directory.Exists(JsScriptDirectory))
        {
            Directory.CreateDirectory(JsScriptDirectory);
            // we will only create this js script if folder does not exist
            string defaultScriptPath = Path.Combine(JsScriptDirectory, DefaultJsScript);
            string defaultScriptContent = @"function performCheck(r) { return alert(""all your Chromapper JavaScripts are now all loaded/moved into: \n 'chromapper\\Plugins\\CM-JS-Scripts'. ;)""),null}module.exports={name:""CM-ScriptUpdateNotice"",params:{},run:performCheck};";
            File.WriteAllText(defaultScriptPath, defaultScriptContent);
        }
    }

    // TODO: allow an option to perhaps specify own folder for javascripts to be loaded from?
    private void LoadJavaScripts(string directory)
    {
        // loads javascripts from a new folder inside of plugins folder. AFTER  4 YEARS! Structure i guess
        foreach (string file in Directory.GetFiles(directory, "*.js"))
        {
            checks.Add(new ExternalJS(file));
        }
        
        // Relocated here for some reason
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
        int mapVersion  = Settings.Instance.MapVersion;

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

        if (mapVersion == 3)
        {
            // Convert manually,
            var allNotes = notesContainer.MapObjects.Where(
                it => it is BaseNote baseNote && baseNote.Type != 3).OrderBy(it => it.JsonTime).ToList();
            var allBombs = notesContainer.MapObjects.Where(
                it => it is BaseNote baseNote && baseNote.Type is 3).OrderBy(it => it.JsonTime).ToList();
            var allArcs = arcsContainer.MapObjects.OrderBy(it => it.JsonTime).ToList();
            var allChains = chainsContainer.MapObjects.OrderBy(it => it.JsonTime).ToList();
            var allWalls = wallsContainer.MapObjects.OrderBy(it => it.JsonTime).ToList();
            var allEvents = eventsContainer.MapObjects.OrderBy(it => it.JsonTime).ToList();
            var allCustomEvents = customEventsContainer.MapObjects.OrderBy(it => it.JsonTime).ToList();
            var allBpmEvents = bpmEventsContainer.MapObjects.OrderBy(it => it.JsonTime).ToList();
            // this try catch could be removed but i kept it for now.
            try
            {
                errors = check.PerformCheck(allNotes, allBombs, allArcs, allChains, allEvents, allWalls, allCustomEvents, allBpmEvents, vals).Commit();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                throw;
            }
            
        } else
        {
            // convert manually like V3 does 
            var allNotes = notesContainer.MapObjects.Where(
                it => it is BaseNote baseNote && baseNote.Type != 3).OrderBy(it => it.JsonTime).ToList();
            var allBombs = notesContainer.MapObjects.Where(
                it => it is BaseNote baseNote && baseNote.Type is 3).OrderBy(it => it.JsonTime).ToList();
            var allWalls = wallsContainer.MapObjects.OrderBy(it => it.JsonTime).ToList();
            var allEvents = eventsContainer.MapObjects.OrderBy(it => it.JsonTime).ToList();
            var allCustomEvents = customEventsContainer.MapObjects.OrderBy(it => it.JsonTime).ToList();
            var allBpmEvents = bpmEventsContainer.MapObjects.OrderBy(it => it.JsonTime).ToList();
            errors = check.PerformCheck(allNotes, allBombs, new List<BaseArc>(), new List<BaseChain>(), allEvents, allWalls, allCustomEvents, allBpmEvents, vals).Commit();
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
