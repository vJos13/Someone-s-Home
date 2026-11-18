// MODE: A = todo con instancing (estado del repo), B = sin instancing (terreno + materiales), C = solo materiales sin instancing
string mode = "__MODE__";
bool terrainInst = mode == "A" || mode == "C";
bool matInst = mode == "A";
var t = UnityEngine.Terrain.activeTerrain; var d = t.terrainData; var protos = d.detailPrototypes;
foreach (var p in protos) p.useInstancing = terrainInst;
d.detailPrototypes = protos;
string[] mats = {"M_Nat_Corteza","M_Nat_FollajeAlamo","M_Nat_CortezaPino","M_Nat_FollajePino","M_Nat_Artemisa","M_Nat_Roca","M_Nat_MaderaCerca","M_Nat_Poste","M_Nat_Vidrio","M_Nat_Metal","M_Nat_Pasto","M_Nat_Juncos"};
foreach (var n in mats) { var m = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Material>("Assets/_Project/Art/Materials/Nature/"+n+".mat"); if (m) m.enableInstancing = matInst; }
t.Flush();
return "mode " + mode + " terrainInst=" + terrainInst + " matInst=" + matInst;
