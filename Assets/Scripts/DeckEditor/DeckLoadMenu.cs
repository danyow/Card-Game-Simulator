﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;

public delegate void OnDeckLoadedDelegate(Deck loadedDeck);

public class DeckLoadMenu : MonoBehaviour
{
    public const string DefaultName = "Untitled";
    public const string SavePrompt = "Would you like to save this deck to file?";
    public const string DeletePrompt = "Are you sure you would like to delete this deck?";

    public RectTransform fileSelectionArea;
    public RectTransform fileSelectionTemplate;
    public Button loadFromFileButton;
    public Button deleteFileButton;
    public InputField nameInputField;
    public TMPro.TextMeshProUGUI instructionsText;
    public TMPro.TMP_InputField textInputField;

    public OnDeckLoadedDelegate LoadCallback { get; private set; }

    public NameChangeDelegate NameChangeCallback { get; private set; }

    public string OriginalName { get; private set; }

    public string SelectedFileName { get; private set; }

    public Deck LoadedDeck { get; private set; }

    public void Show(OnDeckLoadedDelegate loadCallback, NameChangeDelegate nameChangeCallback, string originalName = DefaultName)
    {
        this.gameObject.SetActive(true);
        this.transform.SetAsLastSibling();
        LoadCallback = loadCallback;
        NameChangeCallback = nameChangeCallback;
        OriginalName = originalName;
        SelectedFileName = string.Empty;
        string[] files = Directory.Exists(CardGameManager.Current.DecksFilePath) ? Directory.GetFiles(CardGameManager.Current.DecksFilePath) : new string[0];
        List<string> deckFiles = new List<string>();
        foreach (string fileName in files)
            if (string.Equals(fileName.Substring(fileName.LastIndexOf('.') + 1), CardGameManager.Current.DeckFileType.ToString(), StringComparison.OrdinalIgnoreCase))
                deckFiles.Add(fileName);

        fileSelectionArea.DestroyAllChildren();
        fileSelectionTemplate.SetParent(fileSelectionArea);
        Vector3 pos = fileSelectionTemplate.localPosition;
        pos.y = 0;
        foreach (string deckFile in deckFiles) {
            GameObject deckFileSelection = Instantiate(fileSelectionTemplate.gameObject, fileSelectionArea) as GameObject;
            deckFileSelection.SetActive(true);
            deckFileSelection.transform.localPosition = pos;
            Toggle toggle = deckFileSelection.GetComponent<Toggle>();
            toggle.isOn = false;
            UnityAction<bool> valueChange = new UnityAction<bool>(isOn => SelectFile(isOn, deckFile));
            toggle.onValueChanged.AddListener(valueChange);
            Text labelText = deckFileSelection.GetComponentInChildren<Text>();
            labelText.text = deckFile.Substring(deckFile.LastIndexOf(Path.DirectorySeparatorChar) + 1);
            pos.y -= fileSelectionTemplate.rect.height;
        }
        fileSelectionTemplate.SetParent(fileSelectionArea.parent);
        fileSelectionTemplate.gameObject.SetActive(deckFiles.Count < 1);
        fileSelectionArea.sizeDelta = new Vector2(fileSelectionArea.sizeDelta.x, fileSelectionTemplate.rect.height * deckFiles.Count);

        switch (CardGameManager.Current.DeckFileType) {
            case DeckFileType.Hsd:
                instructionsText.text = Deck.HsdInstructions;
                break;
            case DeckFileType.Ydk:
                instructionsText.text = Deck.YdkInstructions;
                break;
            case DeckFileType.Dec:
            case DeckFileType.Txt:
            default:
                instructionsText.text = Deck.TxtInstructions;
                break;
        }

        nameInputField.text = originalName;
    }

    void Update()
    {
        loadFromFileButton.interactable = !string.IsNullOrEmpty(SelectedFileName);
        deleteFileButton.interactable = !string.IsNullOrEmpty(SelectedFileName);
    }

    public void SelectFile(bool isSelected, string deckFileName)
    {
        if (!isSelected || string.IsNullOrEmpty(deckFileName))
            return;
        
        NameChangeCallback(GetNameFromPath(deckFileName));
        if (deckFileName.Equals(SelectedFileName))
            LoadFromFileAndHide();
        SelectedFileName = deckFileName;
    }

    public string GetNameFromPath(string filePath)
    {
        int startName = filePath.LastIndexOf(Path.DirectorySeparatorChar) + 1;
        int endName = filePath.LastIndexOf('.');
        return filePath.Substring(startName, endName - startName);
    }

    public DeckFileType GetFileTypeFromPath(string filePath)
    {
        DeckFileType fileType = DeckFileType.Txt;
        string extension = filePath.Substring(filePath.LastIndexOf('.') + 1);
        if (extension.ToLower().Equals(DeckFileType.Dec.ToString().ToLower()))
            fileType = DeckFileType.Dec;
        else if (extension.ToLower().Equals(DeckFileType.Hsd.ToString().ToLower()))
            fileType = DeckFileType.Hsd;
        else if (extension.ToLower().Equals(DeckFileType.Ydk.ToString().ToLower()))
            fileType = DeckFileType.Ydk;
        return fileType;
    }

    public void LoadFromFileAndHide()
    {
        string deckText = string.Empty;
        try { 
            deckText = File.ReadAllText(SelectedFileName);
        } catch (Exception e) {
            Debug.LogError("Failed to load deck!: " + e.Message);
            CardGameManager.Instance.Popup.Show("There was an error while attempting to read the deck list from file: " + e.Message);
        }

        Deck newDeck = new Deck(GetNameFromPath(SelectedFileName), GetFileTypeFromPath(SelectedFileName));
        newDeck.Load(deckText);
        LoadCallback(newDeck);
        Hide();
    }

    public void PromptForDeleteFile()
    {
        CardGameManager.Instance.Popup.Prompt(DeletePrompt, DeleteFile);
    }

    public void DeleteFile()
    {
        try { 
            File.Delete(SelectedFileName);
            Show(LoadCallback, NameChangeCallback, OriginalName);
        } catch (Exception e) {
            Debug.LogError("Failed to delete deck!: " + e.Message);
            CardGameManager.Instance.Popup.Show("There was an error while attempting to delete the deck: " + e.Message);
        }
    }

    public void ChangeName(string newName)
    {
        nameInputField.text = NameChangeCallback(newName);
    }

    public void PasteClipboardIntoText()
    {
        textInputField.text = UniClipboard.GetText();
    }

    public void LoadFromTextAndHide()
    {
        Deck newDeck = new Deck(nameInputField.text, CardGameManager.Current.DeckFileType);
        newDeck.Load(textInputField.text);
        LoadCallback(newDeck);
        LoadedDeck = newDeck;
        PromptForSave();
        Hide();
    }

    public void PromptForSave()
    {
        CardGameManager.Instance.Popup.Prompt(SavePrompt, DoSaveNoOverwrite);
    }

    public void DoSaveNoOverwrite()
    {
        if (LoadedDeck == null || !File.Exists(LoadedDeck.FilePath)) {
            DoSave();
            return;
        }
        CardGameManager.Instance.StartCoroutine(WaitToPromptOverwrite());
    }

    public IEnumerator WaitToPromptOverwrite()
    {
        yield return new WaitForSeconds(0.1f);
        CardGameManager.Instance.Popup.Prompt(DeckSaveMenu.OverWriteDeckPrompt, DoSave);
    }

    public void DoSave()
    {
        DeckSaveMenu.SaveToFile(LoadedDeck);
    }

    public void CancelAndHide()
    {
        NameChangeCallback(OriginalName);
        Hide();
    }

    public void Hide()
    {
        this.gameObject.SetActive(false);
    }
}
