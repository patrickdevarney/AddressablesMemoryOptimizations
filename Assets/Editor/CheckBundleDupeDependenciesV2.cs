/// This AnalyzeRule is based on the built-in rule CheckBundleDupeDependencies
/// This rule finds assets in Addressables that will be duplicated across multiple AssetBundles
/// Instead of placing all problematic assets in a shared Group, this rule results in fewer AssetBundles
/// being created by placing assets with the same AssetBundle parents into the same label and AssetBundle
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.Build.Pipeline;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEditor;
using UnityEditor.AddressableAssets.Build.AnalyzeRules;

class CheckBundleDupeDependenciesV2 : MyBundleRuleBase
{
    internal struct CheckDupeResult
    {
        public AddressableAssetGroup Group;
        public string DuplicatedFile;
        public string AssetPath;
        public GUID DuplicatedGroupGuid;
    }

    public override bool CanFix
    {
        get { return true; }
    }

    public override string ruleName
    { get { return "Check Duplicate Bundle Dependencies V2"; } }

    [NonSerialized]
    internal readonly Dictionary<string, Dictionary<string, List<string>>> m_AllIssues = new Dictionary<string, Dictionary<string, List<string>>>();
    [SerializeField]
    internal Dictionary<List<string>, List<string>> duplicateAssetsAndParents = new Dictionary<List<string>, List<string>>();

    public override List<AnalyzeResult> RefreshAnalysis(AddressableAssetSettings settings)
    {
        ClearAnalysis();
        return CheckForDuplicateDependencies(settings);
    }

    List<AnalyzeResult> CheckForDuplicateDependencies(AddressableAssetSettings settings)
    {
        List<AnalyzeResult> retVal = new List<AnalyzeResult>();
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            Debug.LogError("Cannot run Analyze with unsaved scenes");
            retVal.Add(new AnalyzeResult { resultName = ruleName + "Cannot run Analyze with unsaved scenes" });
            return retVal;
        }

        CalculateInputDefinitions(settings);

        if (m_AllBundleInputDefs.Count > 0)
        {
            var context = GetBuildContext(settings);
            ReturnCode exitCode = RefreshBuild(context);
            if (exitCode < ReturnCode.Success)
            {
                Debug.LogError("Analyze build failed. " + exitCode);
                retVal.Add(new AnalyzeResult { resultName = ruleName + "Analyze build failed. " + exitCode });
                return retVal;
            }

            var implicitGuids = GetImplicitGuidToFilesMap();
            var checkDupeResults = CalculateDuplicates(implicitGuids, context);
            BuildImplicitDuplicatedAssetsSet(checkDupeResults);

            retVal = (from issueGroup in m_AllIssues
                      from bundle in issueGroup.Value
                      from item in bundle.Value
                      select new AnalyzeResult
                      {
                          resultName = ruleName + kDelimiter +
                                               issueGroup.Key + kDelimiter +
                                               ConvertBundleName(bundle.Key, issueGroup.Key) + kDelimiter +
                                               item,
                          severity = MessageType.Warning
                      }).ToList();
        }

        if (retVal.Count == 0)
            retVal.Add(noErrors);

        return retVal;
    }

    internal IEnumerable<CheckDupeResult> CalculateDuplicates(Dictionary<GUID, List<string>> implicitGuids, AddressableAssetsBuildContext aaContext)
    {
        //Get all guids that have more than one bundle referencing them
        IEnumerable<KeyValuePair<GUID, List<string>>> validGuids =
            from dupeGuid in implicitGuids
            where dupeGuid.Value.Distinct().Count() > 1
            where IsValidPath(AssetDatabase.GUIDToAssetPath(dupeGuid.Key.ToString()))
            select dupeGuid;

        // Key = bundle parents
        // Value = asset paths that share the same bundle parents
        duplicateAssetsAndParents.Clear();
        foreach (var entry in validGuids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(entry.Key.ToString());
            List<string> assetParents = entry.Value;
            // Purge duplicate parents (assets inside a Scene can show multiple copies of the Scene AssetBundle as a parent)
            List<string> nonDupeParents = new List<string>();
            foreach (var parent in assetParents)
            {
                if (nonDupeParents.Contains(parent))
                    continue;
                nonDupeParents.Add(parent);
            }
            assetParents = nonDupeParents;
            bool found = false;
            // Try to find assetParents in existing dictionary
            foreach (var bundleParentSetup in duplicateAssetsAndParents.Keys)
            {
                if (Enumerable.SequenceEqual(bundleParentSetup, assetParents))
                {
                    duplicateAssetsAndParents[bundleParentSetup].Add(assetPath);
                    found = true;
                    break;
                }
            }
            if (!found)
            {
                duplicateAssetsAndParents.Add(assetParents, new List<string>() { assetPath });
            }
        }

        return
            from guidToFile in validGuids
            from file in guidToFile.Value

                //Get the files that belong to those guids
            let fileToBundle = m_ExtractData.WriteData.FileToBundle[file]

            //Get the bundles that belong to those files
            let bundleToGroup = aaContext.bundleToAssetGroup[fileToBundle]

            //Get the asset groups that belong to those bundles
            let selectedGroup = aaContext.Settings.FindGroup(findGroup => findGroup != null && findGroup.Guid == bundleToGroup)

            select new CheckDupeResult
            {
                Group = selectedGroup,
                DuplicatedFile = file,
                AssetPath = AssetDatabase.GUIDToAssetPath(guidToFile.Key.ToString()),
                DuplicatedGroupGuid = guidToFile.Key
            };
    }

    internal void BuildImplicitDuplicatedAssetsSet(IEnumerable<CheckDupeResult> checkDupeResults)
    {
        foreach (var checkDupeResult in checkDupeResults)
        {
            Dictionary<string, List<string>> groupData;
            // Add the data to the AllIssues container for UI display
            if (!m_AllIssues.TryGetValue(checkDupeResult.Group.Name, out groupData))
            {
                groupData = new Dictionary<string, List<string>>();
                m_AllIssues.Add(checkDupeResult.Group.Name, groupData);
            }

            List<string> assets;
            if (!groupData.TryGetValue(m_ExtractData.WriteData.FileToBundle[checkDupeResult.DuplicatedFile], out assets))
            {
                assets = new List<string>();
                groupData.Add(m_ExtractData.WriteData.FileToBundle[checkDupeResult.DuplicatedFile], assets);
            }

            assets.Add(checkDupeResult.AssetPath);
        }
    }

    public override void FixIssues(AddressableAssetSettings settings)
    {
        // If we have no duplicate data, run the check again
        if (duplicateAssetsAndParents == null || duplicateAssetsAndParents.Count == 0)
            CheckForDuplicateDependencies(settings);

        // If we have found no duplicates, return
        if (duplicateAssetsAndParents.Count == 0)
            return;

        // Setup Addressables Group to store all our duplicate assets
        string desiredGroupName = "Duplicate Assets Sorted By Label";
        AddressableAssetGroup group = settings.FindGroup(desiredGroupName);
        if (group == null)
        {
            group = settings.CreateGroup(desiredGroupName, false, false, false, null, typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
            var bundleSchema = group.GetSchema<BundledAssetGroupSchema>();
            // Set to pack by label so that assets with the same label are put in the same AssetBundle
            bundleSchema.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackTogetherByLabel;
        }

        EditorUtility.DisplayProgressBar("Setting up De-Duplication Group...", "", 0f / duplicateAssetsAndParents.Count);
        // Iterate through each duplicate asset
        int bundleNumber = 1;
        foreach (var entry in duplicateAssetsAndParents)
        {
            EditorUtility.DisplayProgressBar("Setting up De-Duplication Group...", "Creating Label Group", ((float)bundleNumber) / duplicateAssetsAndParents.Count);
            string desiredLabelName = "Bundle" + bundleNumber;
            List<AddressableAssetEntry> entriesToAdd = new List<AddressableAssetEntry>();
            // Put each asset in the shared Group
            foreach (string assetPath in entry.Value)
            {
                entriesToAdd.Add(settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(assetPath).ToString(), group, false, false));
            }

            // Set the label for this selection of assets so they get packed into the same AssetBundle
            settings.AddLabel(desiredLabelName);
            SetLabelValueForEntries(settings, entriesToAdd, desiredLabelName, true);
            bundleNumber++;
        }

        settings.SetDirty(AddressableAssetSettings.ModificationEvent.BatchModification, null, true, true);
    }

    internal void SetLabelValueForEntries(AddressableAssetSettings settings, List<AddressableAssetEntry> entries, string label, bool value, bool postEvent = true)
    {
        if (value)
            settings.AddLabel(label);

        foreach (var e in entries)
            e.SetLabel(label, value, false);

        settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryModified, entries, postEvent, true);
    }

    public override void ClearAnalysis()
    {
        m_AllIssues.Clear();
        duplicateAssetsAndParents.Clear();
        base.ClearAnalysis();
    }
}


[InitializeOnLoad]
class RegisterCheckBundleDupeDependenciesV2
{
    static RegisterCheckBundleDupeDependenciesV2()
    {
        AnalyzeSystem.RegisterNewRule<CheckBundleDupeDependenciesV2>();
    }
}