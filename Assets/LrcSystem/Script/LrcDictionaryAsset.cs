using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class LrcPair
{
    public LrcPair(double time, string word)
    {
        this.time = time;
        this.word = word;
    }
    public double time;
    public string word;
}

[CreateAssetMenu(menuName = "Lrc/LrcWordDictionaryAsset", fileName = "LrcWordDictionaryAsset")]
public class LrcDictionaryAsset : ScriptableObject
{
    // 使用 List 保存键值对数据（相当于 Dictionary 的序列化版本）
    public List<LrcPair> entries = new List<LrcPair>();

    // 如果需要，也可以提供转换为 Dictionary 的方法
    public Dictionary<double, string> ToDictionary()
    {
        Dictionary<double, string> dict = new Dictionary<double, string>();
        foreach (var pair in entries)
        {
            // 注意：如果有重复 key，后面的会覆盖前面的
            dict[pair.time] = pair.word;
        }
        return dict;
    }
}
