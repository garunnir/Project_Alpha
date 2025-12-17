// Copyright (c) Pixel Crushers. All rights reserved.

using UnityEngine;
using System.Collections.Generic;
using UnityEditor;

namespace PixelCrushers.DialogueSystem
{

    /// <summary>
    /// A conversation asset. A conversation is a collection of dialogue entries (see 
    /// MapEntry) that are linked together to form branching, interactive dialogue between two
    /// actors (see Actor).
    /// </summary>
    [System.Serializable]
    public class MapContainer : Asset
    {

        /// <summary>
        /// Optional settings to override the Dialogue Manager's Display Settings.
        /// </summary>
        public ConversationOverrideDisplaySettings overrideSettings = new ConversationOverrideDisplaySettings();

        /// <summary>
        /// Currently unused by the dialogue system, this is the nodeColor value defined in Chat 
        /// Mapper.
        /// </summary>
        public string nodeColor = null;

        /// <summary>
        /// The dialogue entries in the conversation.
        /// </summary>
        public List<MapEntry> mapEntries = new List<MapEntry>();

        public List<EntryGroup> entryGroups = new List<EntryGroup>();

        /// <summary>
        /// Conversation's scroll position in Dialogue Editor window canvas.
        /// </summary>
        [HideInInspector]
        public Vector2 canvasScrollPosition = Vector2.zero;

        /// <summary>
        /// Conversation's zoom level in Dialogue Editor window.
        /// </summary>
        [HideInInspector]
        public float canvasZoom = 1;

        /// <summary>
        /// Gets or sets the Title field.
        /// </summary>
        /// <value>
        /// The title of the conversation, most often used to look up and start a specific 
        /// conversation.
        /// </value>
        public string Title
        {
            get { return LookupValue(DialogueSystemFields.Title); }
            set { Field.SetValue(fields, DialogueSystemFields.Title, value); }
        }

        /// <summary>
        /// Gets or sets the Actor ID. The actor is the primary participant in the conversation.
        /// </summary>
        /// <value>
        /// The actor ID.
        /// </value>
        public int ActorID
        {
            get { return LookupInt(DialogueSystemFields.Actor); }
            set { Field.SetValue(fields, DialogueSystemFields.Actor, value.ToString(), FieldType.Actor); }
        }

        /// <summary>
        /// Gets or sets the Conversant ID. The conversant is the other participant in the 
        /// conversation.
        /// </summary>
        /// <value>
        /// The conversant ID.
        /// </value>
        public int ConversantID
        {
            get { return LookupInt(DialogueSystemFields.Conversant); }
            set { Field.SetValue(fields, DialogueSystemFields.Conversant, value.ToString(), FieldType.Actor); }
        }

        /// <summary>
        /// Initializes a new Conversation.
        /// </summary>
        public MapContainer() { }

        public MapContainer(MapContainer sourceConversation) : base(sourceConversation as Asset)
        {
            this.nodeColor = sourceConversation.nodeColor;
            this.overrideSettings = sourceConversation.overrideSettings;
            this.mapEntries = CopyMapEntries(sourceConversation.mapEntries);
            this.entryGroups = CopyEntryGroups(sourceConversation.entryGroups);
        }
        //private MapContainer CreateMapContainer()
        //{
            //int mapContID=0;
            //string title = FakeConversationTitle;

            //var mapContainer = new MapContainer();
            //mapContainer.id = mapContID;
            //mapContainer.fields = new List<Field>();
            //mapContainer.fields.Add(new Field("Title", title, FieldType.Text));
            //var entry = new MapEntry();
            //entry.conversationID = mapContID;
            //entry.id = 0;
            //entry.fields = new List<Field>();
            //entry.Sequence = "None()";
            //entry.outgoingLinks.Add(new Link(mapContID, 0, mapContID, 1));
            //mapContainer.mapEntries.Add(entry);
            //entry = new MapEntry();
            //entry.conversationID = mapContID;
            //entry.fields = new List<Field>();
            //entry.id = 1;
            //entry.DialogueText = " ";
            //entry.Sequence = "Delay(2)";
            //mapContainer.mapEntries.Add(entry);
            //return mapContainer;
        //}

        /// <summary>
        /// Initializes a new Conversation copied from a Chat Mapper conversation.
        /// </summary>
        /// <param name='chatMapperConversation'>
        /// The Chat Mapper conversation.
        /// </param>
        public MapContainer(ChatMapper.Conversation chatMapperConversation, bool putEndSequenceOnLastSplit = true)
        {
            Assign(chatMapperConversation, putEndSequenceOnLastSplit);
        }
        /// <summary>
        /// Copies a Chat Mapper conversation.
        /// </summary>
        /// <param name='chatMapperConversation'>
        /// The Chat Mapper conversation.
        /// </param>
        public void Assign(ChatMapper.Conversation chatMapperConversation, bool putEndSequenceOnLastSplit = true)
        {
            if (chatMapperConversation != null)
            {
                Assign(chatMapperConversation.ID, chatMapperConversation.Fields);
                nodeColor = chatMapperConversation.NodeColor;
                foreach (var chatMapperEntry in chatMapperConversation.DialogEntries)
                {
                    AddConversationMapEntry(chatMapperEntry);
                }
                SplitPipesIntoEntries(putEndSequenceOnLastSplit);

                // Set priority of links to the destination entry's priority:
                foreach (var entry in mapEntries)
                {
                    foreach (var link in entry.outgoingLinks)
                    {
                        if (link.destinationConversationID != id) continue;
                        var dest = GetMapEntry(link.destinationDialogueID);
                        if (dest == null) continue;
                        link.priority = dest.conditionPriority;
                    }
                }
            }
        }

        /// <summary>
        /// Adds the conversation dialogue entry. Starting in Chat Mapper 1.6, XML entries don't 
        /// include the conversation ID, so we set it manually here.
        /// </summary>
        /// <param name='chatMapperEntry'>
        /// Chat Mapper entry.
        /// </param>
        private void AddConversationMapEntry(ChatMapper.DialogEntry chatMapperEntry)
        {
            var entry = new MapEntry(chatMapperEntry);
            entry.MapID = id;
            mapEntries.Add(entry);
        }

        /// <summary>
        /// Looks up a dialogue entry by title.
        /// </summary>
        /// <returns>
        /// The dialogue entry whose title matches, or <c>null</c> if no such entry exists.
        /// </returns>
        /// <param name='title'>
        /// The title of the dialogue entry.
        /// </param>
        public MapEntry GetMapEntry(string title)
        {
            return mapEntries.Find(e => string.Equals(e.Title, title));
        }

        /// <summary>
        /// Looks up a dialogue entry by its ID.
        /// </summary>
        /// <returns>
        /// The dialogue entry whose Id matches, or <c>null</c> if no such entry exists.
        /// </returns>
        /// <param name='MapEntryID'>
        /// The dialogue entry ID.
        /// </param>
        public MapEntry GetMapEntry(int MapEntryID)
        {
            return mapEntries.Find(e => e.id == MapEntryID);
        }

        /// <summary>
        /// Looks up the first dialogue entry in the conversation, defined (as in Chat Mapper) as 
        /// the entry titled START.
        /// </summary>
        /// <returns>
        /// The first dialogue entry in the conversation.
        /// </returns>
        public MapEntry GetFirstMapEntry()
        {
            return mapEntries.Find(e => string.Equals(e.Title, "START"));
        }

        /// <summary>
        /// Processes all dialogue entries, splitting entries containing pipe characters ("|")
        /// into multiple entries.
        /// </summary>
        /// <param name="putEndSequenceOnLastSplit">
        /// Put sequencer commands with end keyword on the last split entry, other commands on the
        /// first entry, and use default delay for middle entries.
        /// </param>
        /// <param name="trimWhitespace">Trim whitespace such as newlines.</param>
        public void SplitPipesIntoEntries(bool putEndSequenceOnLastSplit = true, bool trimWhitespace = false)
        {
            if (mapEntries != null)
            {
                var count = mapEntries.Count;
                for (int entryIndex = 0; entryIndex < count; entryIndex++)
                {
                    var dialogueText = mapEntries[entryIndex].DialogueText;
                    if (!string.IsNullOrEmpty(dialogueText))
                    {
                        if (dialogueText.Contains("|"))
                        {
                            SplitEntryAtPipes(entryIndex, dialogueText, putEndSequenceOnLastSplit, trimWhitespace);
                        }
                    }
                }
            }
        }

        private void SplitEntryAtPipes(int originalEntryIndex, string dialogueText, bool putEndSequenceOnLastSplit, bool trimWhitespace)
        {
            // Split by Dialogue Text:
            var substrings = dialogueText.Split(new char[] { '|' });
            var originalEntry = mapEntries[originalEntryIndex];
            originalEntry.DialogueText = trimWhitespace ? substrings[0].Trim() : substrings[0];
            var originalOutgoingLinks = originalEntry.outgoingLinks;
            ConditionPriority priority = ((originalOutgoingLinks != null) && (originalOutgoingLinks.Count > 0)) ? originalOutgoingLinks[0].priority : ConditionPriority.Normal;
            var currentEntry = originalEntry;
            var entries = new List<MapEntry>();
            entries.Add(currentEntry);

            // Split Menu Text:
            var defaultMenuText = (originalEntry != null && originalEntry.MenuText != null) ? originalEntry.MenuText : string.Empty;
            var menuTextSubstrings = defaultMenuText.Split(new char[] { '|' });

            // Split Audio Files:
            var audioFilesText = originalEntry.AudioFiles;
            audioFilesText = ((audioFilesText != null) && (audioFilesText.Length >= 2)) ? audioFilesText.Substring(1, audioFilesText.Length - 2) : string.Empty;
            var audioFiles = audioFilesText.Split(new char[] { ';' });
            currentEntry.AudioFiles = string.Format("[{0}]", new System.Object[] { (audioFiles.Length > 0) ? audioFiles[0] : string.Empty });

            // Create new dialogue entries for the split parts:
            int i = 1;
            while (i < substrings.Length)
            {
                var newEntryDialogueText = substrings[i];
                var newEntryMenuText = (i < menuTextSubstrings.Length) ? menuTextSubstrings[i] : string.Empty;
                if (trimWhitespace)
                {
                    newEntryDialogueText = newEntryDialogueText.Trim();
                    newEntryMenuText = newEntryMenuText.Trim();
                }
                var newEntry = AddNewMapEntry(originalEntry, newEntryDialogueText, i, trimWhitespace);
                newEntry.canvasRect = new Rect(originalEntry.canvasRect.x + i * 20, originalEntry.canvasRect.y + i * 10, originalEntry.canvasRect.width, originalEntry.canvasRect.height);
                newEntry.currentMenuText = newEntryMenuText;
                newEntry.AudioFiles = string.Format("[{0}]", new System.Object[] { (i < audioFiles.Length) ? audioFiles[i] : string.Empty });
                currentEntry.outgoingLinks = new List<Link>() { NewLink(currentEntry, newEntry, priority) };
                currentEntry = newEntry;
                entries.Add(newEntry);
                i++;
            }

            // Set the last entry's links to the original outgoing links:
            currentEntry.outgoingLinks = originalOutgoingLinks;

            // Fix up the other splittable fields in the original entry:
            foreach (var field in originalEntry.fields)
            {
                if (string.IsNullOrEmpty(field.title)) continue;
                string fieldValue = (field.value != null) ? field.value : string.Empty;
                bool isSequence = field.title.StartsWith(DialogueSystemFields.Sequence);
                bool isLocalization = (field.type == FieldType.Localization);
                bool containsPipes = fieldValue.Contains("|");
                bool isSplittable = (isSequence || isLocalization) &&
                    !string.IsNullOrEmpty(field.value) && containsPipes;
                if (isSplittable)
                {
                    substrings = field.value.Split(new char[] { '|' });
                    if (substrings.Length > 1)
                    {
                        fieldValue = trimWhitespace ? substrings[0].Trim() : substrings[0];
                        field.value = fieldValue;
                    }
                }
                else if (isSequence && putEndSequenceOnLastSplit && !containsPipes)
                {
                    if (!string.IsNullOrEmpty(field.value) && field.value.Contains(SequencerKeywords.End))
                    {
                        PutEndSequenceOnLastSplit(entries, field);
                    }
                }
            }
        }

        private void PutEndSequenceOnLastSplit(List<MapEntry> entries, Field field)
        {
            var commands = field.value.Split(new char[] { ';' });
            for (int entryNum = 0; entryNum < entries.Count; entryNum++)
            {
                var entry = entries[entryNum];
                var entryField = Field.Lookup(entry.fields, field.title);
                entryField.value = string.Empty;
                if (entryNum == 0)
                {
                    foreach (var command in commands)
                    {
                        if (!command.Contains(SequencerKeywords.End))
                        {
                            entryField.value += command.Trim() + "; ";
                        }
                    }
                    entryField.value += SequencerKeywords.DelayEndCommand;
                }
                else if (entryNum == (entries.Count - 1))
                {
                    foreach (var command in commands)
                    {
                        if (command.Contains(SequencerKeywords.End))
                        {
                            entryField.value += command.Trim() + "; ";
                        }
                    }
                }
                else
                {
                    entryField.value = SequencerKeywords.DelayEndCommand;
                }
            }
        }

        private MapEntry AddNewMapEntry(MapEntry originalEntry, string dialogueText, int partNum, bool trimWhitespace)
        {
            var newEntry = new MapEntry();
            newEntry.id = GetHighestMapEntryID() + 1;
            newEntry.MapID = originalEntry.MapID;
            newEntry.isRoot = originalEntry.isRoot;
            newEntry.isGroup = originalEntry.isGroup;
            newEntry.nodeColor = originalEntry.nodeColor;
            newEntry.delaySimStatus = originalEntry.delaySimStatus;
            newEntry.falseConditionAction = originalEntry.falseConditionAction;
            newEntry.conditionsString = string.Equals(originalEntry.falseConditionAction, "Passthrough") ? originalEntry.conditionsString : string.Empty;
            newEntry.userScript = string.Empty;
            newEntry.fields = new List<Field>();
            foreach (var field in originalEntry.fields)
            {
                if (string.IsNullOrEmpty(field.title)) continue;
                string fieldValue = field.value;
                bool isSplittable = (field.title.StartsWith(DialogueSystemFields.Sequence) || (field.type == FieldType.Localization)) &&
                    !string.IsNullOrEmpty(field.value) && field.value.Contains("|");
                if (isSplittable)
                {
                    string[] substrings = field.value.Split(new char[] { '|' });
                    if (partNum < substrings.Length)
                    {
                        fieldValue = trimWhitespace ? substrings[partNum].Trim() : substrings[partNum].Trim();
                    }
                }
                newEntry.fields.Add(new Field(field.title, fieldValue, field.type));
            }
            newEntry.DialogueText = dialogueText;
            mapEntries.Add(newEntry);
            return newEntry;
        }

        private int GetHighestMapEntryID()
        {
            int highest = 0;
            foreach (var entry in mapEntries)
            {
                highest = Mathf.Max(entry.id, highest);
            }
            return highest;
        }

        private Link NewLink(MapEntry origin, MapEntry destination, ConditionPriority priority = ConditionPriority.Normal)
        {
            var newLink = new Link();
            newLink.originConversationID = origin.MapID;
            newLink.originDialogueID = origin.id;
            newLink.destinationConversationID = destination.MapID;
            newLink.destinationDialogueID = destination.id;
            newLink.isConnector = (origin.MapID != destination.MapID);
            newLink.priority = priority;
            return newLink;
        }

        private List<MapEntry> CopyMapEntries(List<MapEntry> sourceEntries)
        {
            var entries = new List<MapEntry>();
            foreach (var sourceEntry in sourceEntries)
            {
                entries.Add(new MapEntry(sourceEntry));
            }
            return entries;
        }

        private List<EntryGroup> CopyEntryGroups(List<EntryGroup> sourceGroups)
        {
            var groups = new List<EntryGroup>();
            foreach (var group in sourceGroups)
            {
                groups.Add(new EntryGroup(group));
            }
            return groups;
        }


    }

}
