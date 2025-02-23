using System.Collections.Generic;
using LrcSystem.Script;
using UnityEditor.AssetImporters;
using UnityEngine;

[ScriptedImporter(1, "lrc")]
public class LrcAssetImporter : ScriptedImporter
{
    public override void OnImportAsset(AssetImportContext ctx)
    {
        var lrcPath = ctx.assetPath;
        List<LrcPair> lrcPairs = LrcLoader.LoadLrc(lrcPath);

        // 创建 LrcDictionaryAsset 实例
        LrcDictionaryAsset lrcDictionaryAsset = ScriptableObject.CreateInstance<LrcDictionaryAsset>();
        lrcDictionaryAsset.entries = lrcPairs;

        // 将 LrcDictionaryAsset 添加到导入上下文
        ctx.AddObjectToAsset("LrcDictionaryAsset", lrcDictionaryAsset);
        ctx.SetMainObject(lrcDictionaryAsset);
        
    }
}
