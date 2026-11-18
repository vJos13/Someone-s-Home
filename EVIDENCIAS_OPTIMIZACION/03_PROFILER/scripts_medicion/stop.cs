var o = (object[])System.AppDomain.CurrentDomain.GetData("optm");
var recs = (System.Collections.Generic.Dictionary<string, Unity.Profiling.ProfilerRecorder>)o[0];
var stats = (System.Collections.Generic.List<int[]>)o[1];
UnityEditor.EditorApplication.update -= (UnityEditor.EditorApplication.CallbackFunction)o[2];
int f0 = (int)o[3]; double t0 = (double)o[4];
int frames = UnityEngine.Time.frameCount - f0; double secs = UnityEngine.Time.realtimeSinceStartupAsDouble - t0;
var sb = new System.Text.StringBuilder("{");
sb.Append("\"frames\":" + frames + ",\"secs\":" + secs.ToString(System.Globalization.CultureInfo.InvariantCulture) + ",\"fps\":" + (frames/secs).ToString(System.Globalization.CultureInfo.InvariantCulture));
foreach (var kv in recs) {
  var r = kv.Value; int n = r.Count; double sum = 0; long mx = long.MinValue; long mn = long.MaxValue; var vals = new System.Collections.Generic.List<long>();
  for (int i = 0; i < n; i++) { long v = r.GetSample(i).Value; sum += v; if (v>mx) mx=v; if (v<mn) mn=v; vals.Add(v);} 
  vals.Sort(); double med = n>0 ? vals[n/2] : 0; double p95 = n>0 ? vals[(int)(n*0.95)] : 0;
  sb.Append(",\"" + kv.Key + "\":{\"n\":" + n + ",\"avg\":" + (n>0? (sum/n):0).ToString(System.Globalization.CultureInfo.InvariantCulture) + ",\"med\":" + med + ",\"p95\":" + p95 + ",\"min\":" + (n>0?mn:0) + ",\"max\":" + (n>0?mx:0) + "}");
  r.Dispose();
}
string[] sn = {"drawCalls","setPassCalls","srpBatcherDrawCalls","standardDrawCalls","standardInstancedDrawCalls","standardInstancedInstances","instancedBatches","instancedBatchedDrawCalls","staticBatches","staticBatchedDrawCalls","instances","triangles","vertices","shadowCasters"};
for (int k = 0; k < sn.Length; k++) { double s = 0; foreach (var a in stats) s += a[k]; sb.Append(",\"stats_" + sn[k] + "\":" + (stats.Count>0? s/stats.Count:0).ToString(System.Globalization.CultureInfo.InvariantCulture)); }
sb.Append(",\"statsSamples\":" + stats.Count + "}");
System.AppDomain.CurrentDomain.SetData("optm", null);
return sb.ToString();
