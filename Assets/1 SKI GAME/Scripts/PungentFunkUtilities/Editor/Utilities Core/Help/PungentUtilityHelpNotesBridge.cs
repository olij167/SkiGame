namespace PungentFunk.Utilities.Editor.Core.Help
{
#if UNITY_EDITOR
    using System;
    using System.Collections.Generic;
    using PungentFunk.Utilities.Editor.Core;

    public static class PungentUtilityHelpNotesBridge
    {
        public static event Action Changed;

        public static Func<PungentUtilityHelpTopic, string> CreateNoteFromTopic;
        public static Func<PungentUtilityHelpTopic, string, string, string> CreateNoteForAnchor;
        public static Func<PungentUtilityHelpTopic, string, string> CreateBookmark;
        public static Action<PungentUtilityHelpTopic> LinkExistingNote;
        public static Action<PungentUtilityHelpTopic> OpenRelatedNotes;
        public static Action<string> OpenNote;
        public static Func<string, List<PungentUtilityHelpAnnotation>> GetAnnotationsForTopic;
        public static Func<List<PungentUtilityHelpAnnotation>> GetAllHelpAnnotations;
        public static Func<string, List<PungentUtilityHelpAnnotation>> GetHelpAnnotationsForUtility;
        public static Func<int> CountAllHelpNotes;
        public static Action OpenAllHelpNotes;
        public static Func<string, PungentUtilityHelpTopic> PromoteNoteToHelpTopic;
        public static Func<PungentUtilityDocumentationLinks.DocumentationLink, string> CreateNoteFromDocumentationLink;
        public static Action<string> AttachDocumentationLinkToSelectedNote;
        public static Action<string> OpenNotesForDocumentationLink;
        public static Func<string, int> CountNotesForDocumentationLink;

        public static bool Available => CreateNoteFromTopic != null ||
                                        CreateNoteForAnchor != null ||
                                        CreateBookmark != null ||
                                        LinkExistingNote != null ||
                                        OpenRelatedNotes != null ||
                                        OpenNote != null ||
                                        GetAnnotationsForTopic != null ||
                                        GetAllHelpAnnotations != null ||
                                        GetHelpAnnotationsForUtility != null ||
                                        CountAllHelpNotes != null ||
                                        OpenAllHelpNotes != null ||
                                        PromoteNoteToHelpTopic != null ||
                                        CreateNoteFromDocumentationLink != null ||
                                        AttachDocumentationLinkToSelectedNote != null ||
                                        OpenNotesForDocumentationLink != null ||
                                        CountNotesForDocumentationLink != null;

        public static void Clear()
        {
            Changed = null;
            CreateNoteFromTopic = null;
            CreateNoteForAnchor = null;
            CreateBookmark = null;
            LinkExistingNote = null;
            OpenRelatedNotes = null;
            OpenNote = null;
            GetAnnotationsForTopic = null;
            GetAllHelpAnnotations = null;
            GetHelpAnnotationsForUtility = null;
            CountAllHelpNotes = null;
            OpenAllHelpNotes = null;
            PromoteNoteToHelpTopic = null;
            CreateNoteFromDocumentationLink = null;
            AttachDocumentationLinkToSelectedNote = null;
            OpenNotesForDocumentationLink = null;
            CountNotesForDocumentationLink = null;
        }

        public static void NotifyChanged()
        {
            Changed?.Invoke();
        }
    }
#endif
}
