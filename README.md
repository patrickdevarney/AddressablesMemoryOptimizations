# AddressablesMemoryOptimizations

*Link blog post*

A small sample project demonstrating how organized AssetBundles can reduce your Unity project's runtime memory.

Included is an example scene demonstrating memory consumtion with/without Addressables. Also included is a modified version of an Addressables AnalyzeRule that outputs fewer AssetBundles by assigned grouped labels for de-duplicated assets that have the same set of AssetBundle parents that are dependent on them. Fewere bundles loaded at runtime = less SerializedFile memory consumed for AssetBundle metadata.
