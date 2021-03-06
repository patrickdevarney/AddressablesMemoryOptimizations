# AddressablesMemoryOptimizations

[Official Unity Blog post](https://blogs.unity3d.com/2021/03/31/tales-from-the-optimization-trenches-saving-memory-with-addressables/)

A small sample project demonstrating how organized AssetBundles can reduce your Unity project's runtime memory.

Included is an example scene demonstrating memory consumption with/without Addressables. Also included is a modified version of an Addressables AnalyzeRule that outputs fewer AssetBundles by assigning grouped labels for de-duplicated assets that have the same set of AssetBundle parents that are dependent on them. Fewer bundles loaded at runtime = less SerializedFile memory consumed for AssetBundle metadata.

The modified AnalyzeRule can be [viewed here](https://github.com/patrickdevarney/AddressablesMemoryOptimizations/blob/main/Assets/Editor/CheckBundleDupeDependenciesV2.cs).
