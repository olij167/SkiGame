namespace PungentFunk.Utilities.Editor.Core
{
#if UNITY_EDITOR
    using System;
    using System.Collections.Generic;
    using UnityEditor;
    using UnityEngine;

    [Serializable]
    public sealed class PungentUtilityWelcomeLink
    {
        public bool enabled = true;
        public string label = "New Link";
        public string url = string.Empty;
    }

    [Serializable]
    public sealed class PungentUtilityWelcomeSection
    {
        public bool enabled = true;
        public string title = "New Section";
        [TextArea(2, 5)] public string body = string.Empty;
        public List<PungentUtilityWelcomeLink> links = new List<PungentUtilityWelcomeLink>();
    }

    public enum PungentFeedbackType
    {
        BugReport,
        FeatureRequest
    }

    [FilePath("ProjectSettings/PungentFunkUtilities/ExternalLinks.asset", FilePathAttribute.Location.ProjectFolder)]
    public sealed class PungentUtilityExternalLinksSettings : ScriptableSingleton<PungentUtilityExternalLinksSettings>
    {
        public string supportDevelopmentUrl;
        public string websiteUrl;
        public string documentationUrl;
        public string communityUrl;
        public string contactUrl;
        public string bugReportUrl;
        public string featureRequestUrl;
        public string publisherPageUrl;
        public List<PungentUtilityWelcomeSection> welcomeSections = new List<PungentUtilityWelcomeSection>();

        public static bool IsValidAbsoluteUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return false;

            return Uri.TryCreate(url.Trim(), UriKind.Absolute, out Uri parsed) &&
                   (string.Equals(parsed.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(parsed.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase));
        }

        public bool TryOpenSupportDevelopmentUrl(out string message)
        {
            return TryOpenUrl(supportDevelopmentUrl, "Support Development", out message);
        }

        public bool TryOpenFeedback(PungentFeedbackType type, out string message)
        {
            switch (type)
            {
                case PungentFeedbackType.FeatureRequest:
                    return TryOpenUrl(featureRequestUrl, "Feature Request", out message);
                default:
                    return TryOpenUrl(bugReportUrl, "Bug Report", out message);
            }
        }

        public static bool TryOpenUrl(string url, string label, out string message)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                message = label + " link not configured.";
                return false;
            }

            if (!IsValidAbsoluteUrl(url))
            {
                message = label + " link is not a valid absolute URL.";
                return false;
            }

            Application.OpenURL(url.Trim());
            message = "Opened " + label + ".";
            return true;
        }

        public void SaveStore()
        {
            supportDevelopmentUrl = supportDevelopmentUrl ?? string.Empty;
            websiteUrl = websiteUrl ?? string.Empty;
            documentationUrl = documentationUrl ?? string.Empty;
            communityUrl = communityUrl ?? string.Empty;
            contactUrl = contactUrl ?? string.Empty;
            bugReportUrl = bugReportUrl ?? string.Empty;
            featureRequestUrl = featureRequestUrl ?? string.Empty;
            publisherPageUrl = publisherPageUrl ?? string.Empty;
            NormalizeWelcomeSections();
            Save(true);
        }

        private void NormalizeWelcomeSections()
        {
            if (welcomeSections == null)
                welcomeSections = new List<PungentUtilityWelcomeSection>();

            welcomeSections.RemoveAll(section => section == null);
            for (int i = 0; i < welcomeSections.Count; i++)
            {
                PungentUtilityWelcomeSection section = welcomeSections[i];
                section.title = section.title ?? string.Empty;
                section.body = section.body ?? string.Empty;
                if (section.links == null)
                    section.links = new List<PungentUtilityWelcomeLink>();

                section.links.RemoveAll(link => link == null);
                for (int j = 0; j < section.links.Count; j++)
                {
                    PungentUtilityWelcomeLink link = section.links[j];
                    link.label = link.label ?? string.Empty;
                    link.url = link.url ?? string.Empty;
                }
            }
        }
    }
#endif
}
