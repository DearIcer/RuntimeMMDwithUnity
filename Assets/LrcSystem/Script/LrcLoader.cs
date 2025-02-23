using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace LrcSystem.Script
{
    public static class LrcLoader
    {
        public static List<LrcPair> LoadLrc(string lrcPath)
        {
            List<LrcPair> lrcPairs = new List<LrcPair>();
            using (StreamReader sr = new StreamReader(lrcPath, System.Text.Encoding.Default))
            {
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    Regex regex = new Regex(@"\[(\d+):(\d+(\.\d+)?)\](.*)");
                    Match match = regex.Match(line);
                    if (match.Success)
                    {
                        double minutes = double.Parse(match.Groups[1].Value);
                        double seconds = double.Parse(match.Groups[2].Value);
                        double time = minutes * 60 + seconds;
                        string lyric = match.Groups[4].Value.Trim();
                        lrcPairs.Add(new LrcPair(time, lyric));
                    }
                }
            }
            return lrcPairs;
        }
    }
}
