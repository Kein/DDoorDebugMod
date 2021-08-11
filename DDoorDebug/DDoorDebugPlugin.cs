using BepInEx;
using HarmonyLib;
using System;
using UnityEngine.SceneManagement;
using UnityEngine;
using System.Runtime.CompilerServices;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using UnityEngine.Rendering;
using static HarmonyLib.AccessTools;
using DDoorDebug.Model;
using DDoorDebug.Extensions;
using static Damageable;
using static DDoorDebug.Model.PluginOptions;
using static DDoorDebug.Model.PluginCache;

namespace DDoorDebug
{
    [BepInPlugin(GUID, NAME, VERSION)]
    public class DDoorDebugPlugin : BaseUnityPlugin
    {
        const string NAME = "DDoorDebugPlugin";
        const string VERSION = "0.1.5";
        const string GUID = "org.bepinex.plugins.ddoordebugkz";
        //-
        public static DDoorDebugPlugin instance { get; private set; }
        public DDoorDebugData DData  { get; private set;}
        public PluginOptions Options { get; private set;}
        public PluginCache Cache { get; private set;}
        private readonly MutableString builder = new MutableString(600, true);

        // GUI
        private Matrix4x4 guiMatrix;
        private Vector2 SMscroll;
        private Material GLinesMaterial;
        private Material MeshMaterial;
        private Material BoxMaterial;
        private int sceneIndex = 0;
        private float tickFrameTime;
        private string guiOutputStr = "";
        private readonly RectOffset graphOffset = new RectOffset(0, 0, 0, 0);

        // Reflection access
        public static FieldRef<_ChargeWeapon, float> chargedPower = FieldRefAccess<_ChargeWeapon, float>("chargedPower");
        public static FieldRef<Damageable, float> currentHealth = FieldRefAccess<Damageable, float>("currentHealth");
        public static FieldRef<DamageableCharacter, bool> calledDie = FieldRefAccess<DamageableCharacter, bool>("calledDie");
        public static FieldRef<CameraRotationControl, float> angle = FieldRefAccess<CameraRotationControl, float>("angle");
        public static FieldRef<FovZoom, float> currentBaseFov = FieldRefAccess<FovZoom, float>("currentBaseFov");
        public static FieldRef<PlayerGlobal, PlayerInputControl> input = FieldRefAccess<PlayerGlobal, PlayerInputControl>("input");
        
        private void Awake()
        {
            instance = this;
            Options = new PluginOptions();
            DData = new DDoorDebugData();
            Cache = new PluginCache();
            PopulateDData();
            PrepareGUI();
            var harmony = new Harmony(GUID);
            harmony.PatchAll(Assembly.GetExecutingAssembly());
         }

        private void PrepareGUI()
		{
            Camera.onPostRender += OnPostRenderCallback;
            SetMatrix(Screen.width, Screen.height);
            GLinesMaterial = new Material(Shader.Find("Hidden/Internal-Colored")) { hideFlags = HideFlags.HideAndDontSave };
            GLinesMaterial.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            GLinesMaterial.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            GLinesMaterial.SetInt("_Cull", (int)CullMode.Off);
            //GLinesMaterial.SetInt("_ZWrite", 1);
            //GLinesMaterial.SetInt("_ZTest", 2);
            var abundle = AssetBundle.LoadFromFile(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "/wfshader.bundle");
            MeshMaterial = new Material(abundle.LoadAsset<Shader>("wireframetransparent")) { hideFlags = HideFlags.HideAndDontSave };
            CopyMesh(GameObject.CreatePrimitive(PrimitiveType.Sphere), ref Cache.sphereMesh);
            //CopyMesh(GameObject.CreatePrimitive(PrimitiveType.Cube), ref Cache.boxMesh);
            abundle.Unload(false);
		}

        private void CopyMesh(GameObject original, ref Mesh target)
        {
            var shared = original.GetComponent<MeshFilter>().sharedMesh;
            target = new Mesh();
            target.SetVertices(shared.vertices);
            target.SetTriangles(shared.triangles, 0);
            target.RecalculateBounds();
            target.RecalculateNormals();
            target.Optimize();
            Destroy(original);
        }

        private void FullSweep(Transform trans)
        {
            var rawColliders = FindObjectsOfType(typeof(Collider));
            if (rawColliders == null || rawColliders.Length < 1) return;
            Cache.ClearColliderCache();
            Plane[] planes = GeometryUtility.CalculateFrustumPlanes(Cache.mainCam);
            for (int i = 0; i < rawColliders.Length; i++)
            {
                var hitCollider = rawColliders[i] as Collider;
                if (hitCollider && GeometryUtility.TestPlanesAABB(planes , hitCollider.bounds))
                    //&& Globals.instance.solidLayers == (Globals.instance.solidLayers | (1 << hitCollider.gameObject.layer)))
                {
                    if (hitCollider is BoxCollider box)
                        Cache.boxData.Add(box);
                    else if (hitCollider is MeshCollider mesh && !hitCollider.isTrigger && mesh.sharedMesh.isReadable)
                        Cache.meshData.Add(mesh);
                    else if (hitCollider is CapsuleCollider capsule && !hitCollider.isTrigger)
                        Cache.capsuleData.Add(new CapsuleData() { mesh = GenerateCapsule(capsule.height, capsule.radius, capsule), collider = capsule });
                    else if (hitCollider is SphereCollider sphere && !sphere.isTrigger)
                        Cache.sphereData.Add(sphere);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Mesh GenerateCapsule(float height, float radius, CapsuleCollider capsule, int segments = 24) 
	    {
            //calculate exact size sowe dont do anys caling later
            radius = radius * Mathf.Max(Mathf.Abs(capsule.transform.lossyScale.x), Mathf.Abs(capsule.transform.lossyScale.x));
		    height = height * capsule.transform.lossyScale.y;

		    if (segments % 2 != 0)
                segments ++;
		
		    // extra vertex on the seam
		    int points = segments + 1;
		
		    // calculate points around a circle
		    float[] pX = new float[points];
		    float[] pZ = new float[points];
		    float[] pY = new float[points];
		    float[] pR = new float[points];
		
		    float calcH = 0f;
		    float calcV = 0f;
		
		    for ( int i = 0; i < points; i ++ )
		    {
			    pX[i] = Mathf.Sin(calcH * Mathf.Deg2Rad); 
			    pZ[i] = Mathf.Cos(calcH * Mathf.Deg2Rad);
			    pY[i] = Mathf.Cos(calcV * Mathf.Deg2Rad); 
			    pR[i] = Mathf.Sin(calcV * Mathf.Deg2Rad); 
			    calcH += 360f / (float)segments;
			    calcV += 180f / (float)segments;
		    }

		    Vector3[] vertices = new Vector3[points * (points + 1)];
		    int ind = 0;
		
		    // Y-offset is half the height minus the diameter
		    float yOff = (height - (radius * 2f)) * 0.5f;
		    if (yOff < 0)
			    yOff = 0;
		
		    // Top Hemisphere
		    int top = Mathf.CeilToInt((float)points * 0.5f);
		    for (int y = 0; y < top; y++) 
		    {
			    for (int x = 0; x < points; x++) 
			    {
				    vertices[ind] = new Vector3(pX[x] * pR[y], pY[y], pZ[x] * pR[y]) * radius;
				    vertices[ind].y = yOff + vertices[ind].y;
				    ind ++;
			    }
		    }
		
		    // Bottom Hemisphere
		    int btm = Mathf.FloorToInt((float)points * 0.5f);
		
		    for (int y = btm; y < points; y++) 
		    {
			    for (int x = 0; x < points; x++) 
			    {
				    vertices[ind] = new Vector3(pX[x] * pR[y], pY[y], pZ[x] * pR[y]) * radius;
				    vertices[ind].y = -yOff + vertices[ind].y;
				    ind ++;
			    }
		    }
		    // - Triangles -
		    int[] triangles = new int[(segments * (segments + 1) * 2 * 3 )];
		
		    for (int y = 0, t = 0; y < segments + 1; y ++) 
		    {
			    for (int x = 0; x < segments; x ++, t += 6) 
			    {
				    triangles[t + 0] = ((y + 0) * (segments + 1)) + x + 0;
				    triangles[t + 1] = ((y + 1) * (segments + 1)) + x + 0;
				    triangles[t + 2] = ((y + 1) * (segments + 1)) + x + 1;
				    triangles[t + 3] = ((y + 0) * (segments + 1)) + x + 1;
				    triangles[t + 4] = ((y + 0) * (segments + 1)) + x + 0;
				    triangles[t + 5] = ((y + 1) * (segments + 1)) + x + 1;
			    }
		    }
            // We could pool meshes but who cares.	
            Mesh mesh = new Mesh();
		    mesh.vertices = vertices;
		    mesh.triangles = triangles;
		    mesh.RecalculateBounds();
		    mesh.RecalculateNormals();
		    mesh.Optimize();
            return mesh;
	    }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void GLDrawBoxCollider(in BoxCollider box)
        {
            Bounds lBounds = new Bounds(box.center, box.size);
            var go = box.gameObject;
            //Ugly? Yes. Do I give a shit? N-no.
            Cache.boxCachePoints[0] = go.transform.TransformPoint(new Vector3(lBounds.min.x, lBounds.min.y, lBounds.min.z));
            Cache.boxCachePoints[1] = go.transform.TransformPoint(new Vector3(lBounds.min.x, lBounds.min.y, lBounds.max.z));
            Cache.boxCachePoints[2] = Cache.boxCachePoints[1];
            Cache.boxCachePoints[3] = go.transform.TransformPoint(new Vector3(lBounds.min.x, lBounds.max.y, lBounds.max.z));
            Cache.boxCachePoints[4] = Cache.boxCachePoints[3];
            Cache.boxCachePoints[5] = go.transform.TransformPoint(new Vector3(lBounds.min.x, lBounds.max.y, lBounds.min.z));
            Cache.boxCachePoints[6] = Cache.boxCachePoints[5];
            Cache.boxCachePoints[7] = Cache.boxCachePoints[0];
            Cache.boxCachePoints[8] = Cache.boxCachePoints[0];
            Cache.boxCachePoints[9] = go.transform.TransformPoint(new Vector3(lBounds.max.x, lBounds.min.y, lBounds.min.z));
            Cache.boxCachePoints[10] = Cache.boxCachePoints[9];
            Cache.boxCachePoints[11] = go.transform.TransformPoint(new Vector3(lBounds.max.x, lBounds.max.y, lBounds.min.z));
            Cache.boxCachePoints[12] = Cache.boxCachePoints[11];
            Cache.boxCachePoints[13] = Cache.boxCachePoints[5];
            Cache.boxCachePoints[14] = Cache.boxCachePoints[3];
            Cache.boxCachePoints[15] = go.transform.TransformPoint(new Vector3(lBounds.max.x, lBounds.max.y, lBounds.max.z));
            Cache.boxCachePoints[16] = Cache.boxCachePoints[15];
            Cache.boxCachePoints[17] = Cache.boxCachePoints[11];
            Cache.boxCachePoints[18] = Cache.boxCachePoints[9];
            Cache.boxCachePoints[19] = go.transform.TransformPoint(new Vector3(lBounds.max.x, lBounds.min.y, lBounds.max.z));
            Cache.boxCachePoints[20] = Cache.boxCachePoints[19];
            Cache.boxCachePoints[21] = Cache.boxCachePoints[15];
            Cache.boxCachePoints[22] = Cache.boxCachePoints[1];
            Cache.boxCachePoints[23] = Cache.boxCachePoints[19];

            for (int j = 0; j < 23; j++)
            {
                GL.Vertex(Cache.boxCachePoints[j]);
                GL.Vertex(Cache.boxCachePoints[++j]);
            }
        }

        private void OnPostRenderCallback(Camera cam)
        {
           if (!CanRender() || cam != Cache.mainCam)
                return;

            // We draw box colliders via GL.Lines because we need quad-like representation
            // and shaders are hard --barbie
            if (Options.collViewMode[(int)ViewMode.Box] == 1 && Cache.boxData.Count > 1)
            {
                GL.PushMatrix();
                GLinesMaterial.SetPass(0);
                GL.MultMatrix(Matrix4x4.identity);
                GL.Begin(GL.LINES);
                for (int i = Cache.boxData.Count - 1; i >= 0; i--)
                {   
                    Color line = Color.red;
                    var box = Cache.boxData[i];
                    if (box == null)
                    {
                        Cache.boxData.RemoveAt(i);
                        continue;
                    }
                    if (box.isTrigger)
                        line = Color.green;
                    if (!box.enabled)
                        line = new Color(0.5f, 0.5f, 0.5f, 0.7f);
                    GL.Color(line);
                    GLDrawBoxCollider(box);
                }
                GL.End();
                GL.PopMatrix();
            }
            // Path trail
            if (Options.posHistGraphEnabled && DData.posHistSamples.Count > 2)
            {
                GL.PushMatrix();
                GLinesMaterial.SetPass(0);
                GL.MultMatrix(Matrix4x4.identity);
                GL.Begin(GL.LINE_STRIP);
                GL.Color(Color.yellow);
                foreach (var pos in DData.posHistSamples)
                {
                    GL.Vertex(pos + new Vector3(0,2.5f,0));
                    GL.Vertex(pos + new Vector3(0,-2f,0));
                    GL.Vertex(pos + new Vector3(0,2.5f,0));
                }
                GL.End();
                GL.PopMatrix();
            }
        }

        //
        // Helpers
        //
        public void SetMatrix(float x, float y) => guiMatrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(x / 1920f, y / 1080f, 1f));

        // Manual scaling for other resolution off base 1920x1080
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float FitX(float orig) => (orig * Screen.width) / 1920f;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float FitY(float orig) => (orig * Screen.height) / 1080f;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool CanRender() => PlayerGlobal.instance != null && DData != null && DData.curActiveScene != "TitleScreen";

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool Toggle(ref bool value) { value = !value; return value; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DrawVelGraph()
        {
            if (DData.velSamples.Count < Options.maxGraphSamples)
                return;

            GL.PushMatrix();
            GLinesMaterial.SetPass(0);
            GL.LoadPixelMatrix();
            GL.Begin(GL.QUADS);
            GL.Color(new Color(0f, 0f, 0f, 0.45f)); //black
            var leftX  = FitX(Options.graphPosGL.x);
            var botY   = FitY(Options.graphPosGL.y);
            var rightX = FitX(Options.graphPosGL.x + PluginOptions.graphWidth);
            var topY   = FitY(Options.graphPosGL.y + PluginOptions.graphHeight);
            GL.Vertex3(leftX, botY, 0);
            GL.Vertex3(rightX, botY, 0);
            GL.Vertex3(rightX, topY, 0);
            GL.Vertex3(leftX, topY, 0);
            GL.End();
            GL.Begin(GL.LINES);
            GL.Color(new Color(1f, 1f, 1f, 0.4f)); // grid lines, gray
            Vector3 start = Options.graphPosGL;
            var step = (Options.frameSampleSize * 1000f);
            // Scale Grid
            for (int i = 1; i < 10; i++)
            {
                GL.Vertex(new Vector3(FitX(start.x), FitY(start.y + (i * 10f)), 0f));
                GL.Vertex(new Vector3(FitX(start.x+330f), FitY(start.y + (i * 10f)), 0f));
            }
            // Plot line
            GL.Color(Color.cyan);
            float vel = Mathf.Min(DData.velSamples.Peek(), 100f);
            start = new Vector3(FitX(start.x), FitY(start.y + vel), 0);
            var c = 0;
            foreach (var speed in DData.velSamples)
            {
                vel = Mathf.Min(speed, 100f);
                var end = new Vector3(FitX(Options.graphPosGL.x + (step * (c+1))), FitY(Options.graphPosGL.y + vel), 0);
                for (int j = 0; j < Options.velLineWidth; j++)
                {
                    GL.Vertex(start+new Vector3(0,0+j,0));
                    GL.Vertex(end+new Vector3(0,0+j,0));
                }
                start = end;
                c++;
            }
            GL.End();
            GL.PopMatrix();
        }

        private void PopulateDData()
		{
            // Switch would be faster but we dont want to fix it manually each update if new value added
            DData.dmgTypes = new Dictionary<int, string>(13);
            foreach (DamageType val in Enum.GetValues(typeof(DamageType)))
                DData.dmgTypes.Add((int)val, val.ToString());
            DData.damageables = new List<DamageableRef>(30);
            DData.velSamples = new Queue<float>(Options.maxGraphSamples);
            DData.posHistSamples = new Queue<Vector3>(Options.maxPosHistSamles);
            DData.lastSave = Time.realtimeSinceStartup;
            var sceneNumber = SceneManager.sceneCountInBuildSettings;
            List<string> scenes = new List<string>(20);
            for (int i = 0; i < sceneNumber; i++)
            {
                string scene = Path.GetFileNameWithoutExtension(SceneUtility.GetScenePathByBuildIndex(i));
                if (scene[0] != '_')
                    scenes.Add(scene);
            }
            DData.allScenes = scenes.OrderByDescending(x => x).ToArray();
		}

        private void FixedUpdate() => SamplePositionHistory();

        private void Update()
        {
            ProcessInput();
            TickLogic();
            SampleData();
            DrawLine();
            DrawMeshColliders();
        }

        // We could rewrite it to be generic but who cares
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DrawMeshColliders()
        {
            if (!CanRender())
                return;

            if (Options.collViewMode[(int)ViewMode.Mesh] == 1 && Cache.meshData.Count > 0)
            {
                Cache.matProps.SetColor("_WireColor", Color.magenta);
                for (int i = Cache.meshData.Count - 1; i >= 0; i--)
                {
                    var mesh = Cache.meshData[i];
                    if (!mesh)
                    {
                        Cache.meshData.RemoveAt(i);
                        continue;
                    }
                    if (!mesh.enabled || !mesh.gameObject.activeSelf)
                        continue;
                    var matrix = Matrix4x4.TRS(mesh.transform.position, mesh.transform.rotation, mesh.transform.lossyScale);
                    Graphics.DrawMesh(mesh.sharedMesh, matrix, MeshMaterial, 32, Cache.mainCam, 0, Cache.matProps);
                }
            }

            if (Options.collViewMode[(int)ViewMode.Capsule] == 1 && Cache.capsuleData.Count > 0)
            {
                Cache.matProps.SetColor("_WireColor", Color.yellow);
                for (int i = Cache.capsuleData.Count - 1; i >= 0; i--)
                {
                    var capsule = Cache.capsuleData[i];
                    if (capsule.collider == null)
                    {
                        Cache.capsuleData.RemoveAt(i);
                        continue;
                    }
                    if (!capsule.collider.enabled || !capsule.collider.gameObject.activeSelf)
                        continue;
                    var matrix = Matrix4x4.TRS(capsule.collider.bounds.center, capsule.collider.transform.rotation, Vector3.one);
                    Graphics.DrawMesh(capsule.mesh, matrix, MeshMaterial, 32, Cache.mainCam, 0, Cache.matProps);
                }
            }

            if (Options.collViewMode[(int)ViewMode.Sphere] == 1 && Cache.sphereData.Count > 0)
            {
                Cache.matProps.SetColor("_WireColor", Color.cyan);
                for (int i = Cache.sphereData.Count - 1; i >= 0; i--)
                {
                    var sphere = Cache.sphereData[i];
                    if (sphere == null)
                    {
                        Cache.capsuleData.RemoveAt(i);
                        continue;
                    }
                    if (!sphere.enabled || !sphere.gameObject.activeSelf)
                        continue;
                    var matrix = Matrix4x4.TRS(sphere.bounds.center, sphere.transform.rotation, sphere.transform.lossyScale * sphere.radius * 2);
                    Graphics.DrawMesh(Cache.sphereMesh, matrix, MeshMaterial, 32, Cache.mainCam, 0, Cache.matProps);
                }
            }
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SamplePositionHistory()
        {
            if (PlayerGlobal.instance != null && Options.posHistGraphEnabled)
            {
                DData.lastPosHisSampleTime += Time.fixedDeltaTime;
                var currPos = PlayerGlobal.instance.transform.position;
                if (currPos != DData.lastPosHistSample && DData.lastPosHisSampleTime > Time.fixedDeltaTime + 0.05f)
                {
                    DData.lastPosHistSample = currPos;
                    DData.lastPosHisSampleTime = 0;
                    if (DData.posHistSamples.Count >= Options.maxPosHistSamles)
                        _ = DData.posHistSamples.Dequeue();
                    DData.posHistSamples.Enqueue(currPos);
                }
            }
        }

        public void ClearAllCache()
        {
            Cache.ClearColliderCache();
            DData.damageables.Clear();
            DData.velSamples.Clear();
            DData.posHistSamples.Clear();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DrawLine()
        {
            if (!Cache.lineRenderer || !Cache.lineRenderer.enabled) return;
            var playerPos = Cache.lineRenderer.transform.position + new Vector3(0f,2.5f,0f);
            Vector3 end = playerPos - (Cache.lineRenderer.transform.forward * -50f);
            Cache.lineCache[0] = playerPos;
            Cache.lineCache[1] = end;
			Cache.lineRenderer.SetPositions(Cache.lineCache);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void TickLogic()
        {
            tickFrameTime += Time.deltaTime;
            if (tickFrameTime < 1f) return;
            tickFrameTime = 0;
            if (Options.autoHeal && DData.dmgObject && DData.dmgObject.GetCurrentHealth() < DData.dmgObject.maxHealth)
                DData.dmgObject.HealToFull();
        }

        private void OnGUI()
        {
            if (!CanRender())
                return;

            Matrix4x4 matrix = GUI.matrix;
            //avoid GIUStyle at all cost - lotsa garbage
            var lFont = GUI.skin.label.fontSize;
            var lStyle = GUI.skin.label.fontStyle;
            var lAlign = GUI.skin.label.alignment;
            var lPadding = GUI.skin.label.padding;
            var bFont = GUI.skin.box.fontSize;
            var bStyle = GUI.skin.box.fontStyle;
            var bAlign = GUI.skin.box.alignment;
            var bPadding = GUI.skin.box.padding;
			GUI.matrix = guiMatrix;

            if (Options.menuEnabled && Event.current.type == EventType.Repaint)
            {
                GUI.skin.box.fontSize = 20;
                GUI.skin.box.fontStyle = FontStyle.Bold;
                GUI.skin.box.alignment = TextAnchor.UpperLeft;
                var box = new Rect(1920f-10f-PluginOptions.guiInfoWidth, 10f, PluginOptions.guiInfoWidth, 550f);
                GUI.Box(box, guiOutputStr);
                if (Options.velGraphEnabled)
                {
                    GUI.skin.label.fontSize = 12;
                    GUI.skin.label.alignment = TextAnchor.UpperRight;
                    GUI.skin.label.padding = graphOffset;
                    GUI.Label(new Rect(1155f, 4f, 30f, 28f), "100");
                    GUI.Label(new Rect(1155f, 53f, 30f, 28f), "50");
                    GUI.Label(new Rect(1155f, 100f, 30f, 28f), "0");
                    DrawVelGraph();
                }
            }
            if (Options.sceneMenuEnabled)
            {
                Cursor.visible = true;
                GUI.skin.button.fontSize = 18;
                GUI.skin.button.fontStyle = FontStyle.Bold;
                var box = new Rect(10f, 10f, 338f, 494f);
                GUI.Box(box, string.Empty);
                var c = DData.allScenes.Length;
                SMscroll = GUI.BeginScrollView(new Rect(box.x + 2f, box.y + 2f, 340f, 458f), SMscroll, new Rect(box.x + 2f, box.y + 2f, 330f, c * 30f), false, true);
                sceneIndex = GUI.SelectionGrid(new Rect(box.x + 3f, box.y + 3f, 330f, c * 30f), sceneIndex, DData.allScenes, 1);
                GUI.EndScrollView();
                if (GUI.Button(new Rect(12f, 470f, 100f, 30f), "<color=yellow>Travel To</color>"))
                {
                    DoorTrigger.currentTargetDoor = "_debug";
                    GameSceneManager.LoadSceneFadeOut(DData.allScenes[sceneIndex], 0.1f, true);
                    Options.sceneMenuEnabled = false;
                    input(PlayerGlobal.instance).PauseInput(Options.sceneMenuEnabled);
                }
            }

            GUI.matrix = matrix;
            if (Options.hpEnabled && Event.current.type == EventType.Repaint && Cache.mainCam != null && DData.damageables.Count > 0)
            {
                for (int i = DData.damageables.Count - 1; i >= 0; i--)
                {
                    var curRef = DData.damageables[i];
                    var current = curRef.instance;
                    if (!current)
                    {
                        DData.damageables[i].stringHealth = null;
                        DData.damageables[i] = null;
                        DData.damageables.RemoveAt(i);
                        continue;
                    }
                    var curhealth = currentHealth(current);
                    if (curhealth > 989f || curhealth < 0.09f || !current.gameObject.activeInHierarchy || !current.gameObject.activeSelf)
                        continue;
                    Vector2 vector = Cache.mainCam.WorldToScreenPoint(current.transform.position);
                    if (vector.x > 0f && vector.x < (float)Screen.width && vector.y > 0f && vector.y < (float)Screen.height)
                    {
                        if (Mathf.Abs(Mathf.Abs(curRef.trackedHealth) - Mathf.Abs(curhealth)) > 0.05f)
                        {
                            curRef.trackedHealth = curhealth;
                            curRef.stringHealth = curhealth.ToString("N1");
                        }
                        GUI.skin.box.fontSize = 20;
                        GUI.skin.box.fontStyle = FontStyle.Bold;
                        GUI.skin.box.alignment = TextAnchor.MiddleCenter;
                        float y = (float)Screen.height - vector.y;
                        GUI.Box(new Rect(vector.x, y, 60f, 22f), curRef.stringHealth);
                    }
                }
            }
             GUI.skin.label.fontSize = lFont;
             GUI.skin.label.fontStyle = lStyle;
             GUI.skin.label.alignment = lAlign;
             GUI.skin.label.padding = lPadding;
             GUI.skin.box.fontSize = bFont;
             GUI.skin.box.fontStyle = bStyle;
             GUI.skin.box.alignment = bAlign;
             GUI.skin.box.padding = bPadding;
        }

        private void SampleData()
        {
            if (CanRender())
            {
                builder.Append("[DEBUG INFO]  <size=16>(").Append(VERSION).Append(")</size>");
                if (Options.menuEnabled)
                {
                    var meleeMult = Inventory.GetMeleeDamageModifier();
                    var magicMod = Inventory.GetMagicDamageModifier();
                    var speed = Inventory.GetSpeedModifier();
                    builder.Append("\nScene: ").Append(DData.curActiveScene);
                    builder.Append("\nMeleeMult: ").Append(meleeMult, 2);
                    builder.Append("\nMeleeRange: ").Append(Inventory.GetMeleeRangeModifier(), 2);
                    builder.Append("\nMagicMult: ").Append(magicMod, 2);
                    builder.Append("\nDexterityMult: ").Append(Inventory.GetDexterityModifier(), 2);
                    builder.Append("\nRollSpeedMult: ").Append(Inventory.GetRollSpeedModifier(), 2);
                    builder.Append("\nSpeedMult: ").Append(speed, 2);

                    if (DData.movCtrlObject != null)
                        builder.Append("\nMaxSpeed: ").Append(DData.movCtrlObject.maxSpeed * PlayerGlobal.instance.speedMultiplier * speed, 2);
                    builder.Append("\n-");

                    if (DData.wpnRefs != null)
                    {
                        SampleWeapon(builder, DData.wpnRefs.lightAttack, meleeMult, "\nLight: ");
                        SampleWeapon(builder, DData.wpnRefs.heavyAttack, meleeMult, "\nHeavy: ");
                        SampleWeapon(builder, DData.wpnRefs.rollAttack, meleeMult, "\nRoll: ");
                        SampleWeapon(builder, DData.wpnRefs.hookshotAttack, meleeMult, "\nHook: ");
                    }
                    if (DData.magicRefs != null)
                    {
                        var inst = DData.magicRefs.GetArrowInstance();
                        var dmg = 0f;
                        var type = DData.magicRefs.GetType().Name;
                        if (inst)
                        {
                            dmg = inst is BombArrow ? 3f : inst.damage;
                            type = DData.dmgTypes[(int)inst.damageType];
                        }
                        builder.Append("\nMagic: ").Append(magicMod * dmg, 2).Append(" (").Append(dmg, 2).Append("*")
                                                .Append(magicMod).Append(")").Append(" [").Append(type).Append("]");
                    }
                    builder.Append("\nPlungingDmg: ").Append(PlayerGlobal.instance.GetPlungingDamage(), 1);
                    builder.Append("\n-");
                    builder.Append("\nLast Damage: \n").Append("[ <color=yellow>").Append(DData.lastDamage.dmg, 2).Append("</color> | ")
                           .Append(DData.lastDamage.poiseDmg, 2).Append(" | ").Append(DData.dmgTypes[(int)DData.lastDamage.type]).Append(" ]");

                    var diff = Time.realtimeSinceStartup - DData.lastSave;
                    string color = diff < 5f ? "\n<color=lime>" : "\n<color=white>";
                    builder.Append(color).Append("Last save: ").Append(diff, 0, 5, ' ').Append("s ago</color>");

                    TimeSpan span = TimeSpan.FromSeconds(GameTimeTracker.instance.GetTime());
                    builder.Append("\nGameTime: ").Append(span.Hours, 3, ' ').Append(":").Append(span.Minutes, 2, '0').Append(":").Append(span.Seconds, 2, '0');

                    var pos = PlayerGlobal.instance.transform.localPosition; // slight diif in Y with global .position
                    builder.Append("\nPos: ").Append("x: ").Append(pos.x, 2, 2, ' ').Append(" y: ").Append(pos.y, 2, 2, ' ').Append(" z: ").Append(pos.z, 2, 2, ' ');

                    if (DData.plrRBody != null)
                    {
                        var vel = DData.plrRBody.velocity.magnitude;
                        SampleVelocity(vel);
                        builder.Append("\nVelocity: ").Append(vel, 2).Append("  Peak: ").Append(DData.lastVelocity, 2);
                    }
                }
                guiOutputStr = builder.Finalize();
            }
        }
            
        private void ProcessInput()
        {
            if (!CanRender()) return;

            if (Input.GetKeyUp(KeyCode.F1))
            {
                Toggle(ref Options.menuEnabled);
                DData.lastVelocity = 0;
            }

            if (Input.GetKeyUp(KeyCode.F2))
            {
                DData.damageables.Clear();
                if (Toggle(ref Options.hpEnabled))
                {
                    var foundDmgbls = FindObjectsOfType<DamageableCharacter>();
                    for (int i = 0; i < foundDmgbls.Length; i++)
                        AddDamageable(foundDmgbls[i]);
                }
            }
            if (Input.GetKeyUp(KeyCode.F3))
                input(PlayerGlobal.instance).PauseInput(Toggle(ref Options.sceneMenuEnabled));

            if (Input.GetKeyUp(KeyCode.F4))
                Toggle(ref Options.autoHeal);

            if (Input.GetKeyUp(KeyCode.F5))
            {
                foreach (var boss_str in DData.bossKeys)
                    GameSave.GetSaveData().SetKeyState(boss_str, false,false);
                if (Input.GetKey(KeyCode.LeftShift))
                    foreach (var boss_str in DData.bossesIntroKeys)
                        GameSave.GetSaveData().SetKeyState(boss_str, false,false);    
            }
            if (Input.GetKeyUp(KeyCode.F6))
            {
                Inventory.instance.AddItem("currency", 30000, false);
                Inventory.instance.SetItemCount("stat_melee", 0);
	            Inventory.instance.SetItemCount("stat_dexterity", 0);
	            Inventory.instance.SetItemCount("stat_haste", 0);
	            Inventory.instance.SetItemCount("stat_magic", 0);
            }
            if (Input.GetKeyUp(KeyCode.F7))
            {
                Inventory.instance.AddItem("daggers", 1, false);
                Inventory.instance.AddItem("hammer", 1, false);
                Inventory.instance.AddItem("sword_heavy", 1, false);
                Inventory.instance.AddItem("umbrella", 1, false);
            }
            if (Input.GetKeyUp(KeyCode.F8))
                DData.lastCheckPoint = new SceneCP() { hash = DData.curActiveScene.GetHashCode(), pos = PlayerGlobal.instance.transform.position };

            if (Input.GetKeyUp(KeyCode.F9))
            {   
                if (DData.lastCheckPoint.hash == DData.curActiveScene.GetHashCode())
                    PlayerGlobal.instance.SetPosition(DData.lastCheckPoint.pos, false, false);
            }

            if (Input.GetKeyUp(KeyCode.F10))
            {
                if (Input.GetKey(KeyCode.LeftControl))
                {
                    FullSweep(PlayerGlobal.instance.transform);
                }
                else
                {
                    Options.collViewMode[Options.cvmPos] = (byte)(1 - Options.collViewMode[Options.cvmPos]);
                    Options.cvmPos = ++Options.cvmPos % Options.collViewMode.Length;
                }
            }

            if (Input.GetKeyUp(KeyCode.F11) && Cache.cineBrain != null)
            {
                Cache.cineBrain.enabled = !Cache.cineBrain.enabled;
                Options.freeCamEnabled = !Cache.cineBrain.enabled;
            }

            if (Input.GetKeyUp(KeyCode.Alpha0))
            {
                if (!Cache.lineRenderer)
                    Cache.lineRenderer = SpawnLineRenderer();
                else
                    Cache.lineRenderer.enabled = !Cache.lineRenderer.enabled;
            }

            if (Input.GetKeyUp(KeyCode.P))
            {
                Toggle(ref Options.posHistGraphEnabled);
                DData.posHistSamples.Clear();
            }

            if (Input.GetKeyUp(KeyCode.Backspace))
                Toggle(ref Options.velGraphEnabled);

            if (Input.GetKeyUp(KeyCode.Insert))
                Time.timeScale = Mathf.Clamp01(Time.timeScale - 0.1f);
            if (Input.GetKeyUp(KeyCode.PageUp))
                Time.timeScale = Mathf.Clamp(Time.timeScale + 0.1f, 0f, 2f);
            if (Input.GetKeyUp(KeyCode.Home))
                Time.timeScale = 1f;

            Buttons.PauseInput(Input.GetKey(KeyCode.LeftControl));
            if (Input.GetKey(KeyCode.LeftControl) && Input.GetKeyUp(KeyCode.Mouse0) && PlayerGlobal.instance != null && !PlayerGlobal.instance.InputPaused())
                    SpawnAtCursor();

            if (Input.GetKeyUp(KeyCode.Delete) && CameraRotationControl.instance)
            {
                var currAngle = angle(CameraRotationControl.instance) + 45f;
                currAngle = currAngle > 315 ? 315 : currAngle;
                CameraRotationControl.instance.Rotate(currAngle, 3);
            }
            if (Input.GetKeyUp(KeyCode.PageDown) && CameraRotationControl.instance)
            {
                var currAngle = angle(CameraRotationControl.instance) - 45f;
                currAngle = currAngle < 0 ? 0 : currAngle;
                CameraRotationControl.instance.Rotate(currAngle,3);
            }
            
            if (Options.freeCamEnabled && Cache.mainCam != null)
            {
                Options.freeLookConf.rotationX += Input.GetAxis("Mouse X") * Options.freeLookConf.cameraSensitivity * Time.deltaTime;
			    Options.freeLookConf.rotationY += Input.GetAxis("Mouse Y") * Options.freeLookConf.cameraSensitivity * Time.deltaTime;
			    Options.freeLookConf.rotationY = Mathf.Clamp (Options.freeLookConf.rotationY, -90, 90);

			    float vAxis = 0f;
			    float hAxis = 0f;
			    float factor = 1;

			    if (Input.GetKey(KeyCode.F))
				    hAxis = -1f;
			    else if (Input.GetKey(KeyCode.H))
				    hAxis = 1f;

			    if (Input.GetKey(KeyCode.T))
				    vAxis = 1f;
			    else if (Input.GetKey(KeyCode.G))
				    vAxis = -1f;

	 		    if (Input.GetKey (KeyCode.LeftShift) || Input.GetKey (KeyCode.RightShift))
				    factor = Options.freeLookConf.fastMoveFactor;
	 		    else if (Input.GetKey (KeyCode.LeftControl) || Input.GetKey (KeyCode.RightControl))
				    factor = Options.freeLookConf.slowMoveFactor;

	 		    Cache.mainCam.transform.position += Cache.mainCam.transform.forward * (Options.freeLookConf.normalMoveSpeed * factor) * vAxis * Time.deltaTime;
			    Cache.mainCam.transform.position += Cache.mainCam.transform.right * (Options.freeLookConf.normalMoveSpeed * factor) * hAxis * Time.deltaTime;

                var currEulerAngles = Cache.mainCam.transform.localRotation;
                if (Input.GetKey(KeyCode.Home))
                    currEulerAngles.y += 2f * Time.deltaTime;
                if (Input.GetKey(KeyCode.End))
                    currEulerAngles.y -= 2f * Time.deltaTime;;
                if (Input.GetKey(KeyCode.Delete))
                    currEulerAngles.x -= 2f * Time.deltaTime;
                if (Input.GetKey(KeyCode.PageDown))
                    currEulerAngles.x += 2f * Time.deltaTime;
                if (Input.GetKey(KeyCode.Insert))
                    currEulerAngles.z -= 2f * Time.deltaTime;
                if (Input.GetKey(KeyCode.PageUp))
                    currEulerAngles.z += 2f * Time.deltaTime;
                if (Options.freeCamMouse)
                {
                    Cache.mainCam.transform.localRotation = Quaternion.AngleAxis(Options.freeLookConf.rotationX, Vector3.up);
			        Cache.mainCam.transform.localRotation *= Quaternion.AngleAxis(Options.freeLookConf.rotationY, Vector3.left);
                }
                else
                {
                    Cache.mainCam.transform.localRotation = currEulerAngles;
                }
                if (Input.GetKeyUp(KeyCode.Minus) || Input.GetKeyUp(KeyCode.KeypadMinus)) { Cache.mainCam.fieldOfView -= 0.5f * Time.deltaTime; }
                if (Input.GetKeyUp(KeyCode.Equals) || Input.GetKeyUp(KeyCode.KeypadPlus)) { Cache.mainCam.fieldOfView += 0.5f * Time.deltaTime; }
			    if (Input.GetKey(KeyCode.R)) { Cache.mainCam.transform.position += Cache.mainCam.transform.up * (Options.freeLookConf.climbSpeed * factor) * Time.deltaTime; }
			    if (Input.GetKey(KeyCode.Y)) { Cache.mainCam.transform.position -= Cache.mainCam.transform.up * (Options.freeLookConf.climbSpeed * factor) * Time.deltaTime;}
            }

            if ((Input.GetKeyUp(KeyCode.Minus) || Input.GetKeyUp(KeyCode.KeypadMinus)) && !Options.freeCamEnabled && FovZoom.instance)
                FovZoom.instance.SetCurrentBaseZoom(currentBaseFov(FovZoom.instance) - 2f);
            if ((Input.GetKeyUp(KeyCode.Equals) || Input.GetKeyUp(KeyCode.KeypadPlus)) && !Options.freeCamEnabled && FovZoom.instance)
                FovZoom.instance.SetCurrentBaseZoom(currentBaseFov(FovZoom.instance) + 2f);
        }

        private LineRenderer SpawnLineRenderer()
        {
            var lines = PlayerGlobal.instance.transform.Find("VISUALS/crow_player (fbx)/body").gameObject.AddComponent<LineRenderer>();
            lines.endWidth = 0.26f;
            lines.startWidth = 0.07f;
            lines.startColor = Color.yellow;
            lines.endColor = Color.red;
            lines.material = GLinesMaterial;
            var colorKeys = new GradientColorKey[2] { new GradientColorKey(Color.yellow, 0.0f), new GradientColorKey(Color.red, 1.0f) };
            var alphaKeys = new GradientAlphaKey[2] { new GradientAlphaKey(1.0f, 0.0f), new GradientAlphaKey(1.0f, 1.0f) };
            lines.colorGradient = new Gradient() { mode = GradientMode.Blend, colorKeys = colorKeys, alphaKeys = alphaKeys };
            lines.positionCount = 2;
            return lines;
        }

        public void AddDamageable(DamageableCharacter dmg)
        {
            if (!Options.hpEnabled) return;
            var curHP = currentHealth(dmg);
            if (curHP > 0.05 && !calledDie(dmg) && dmg.name.IndexOf("ragdoll", StringComparison.OrdinalIgnoreCase) < 0)
                DData.damageables.Add(new DamageableRef(dmg, curHP, curHP.ToString("N1")));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SampleVelocity(float speed)
        {
            if (speed > DData.lastVelocity)
                DData.lastVelocity = speed;
            DData.lastVelSampleTime += Time.deltaTime;
            if (DData.lastVelSampleTime > Options.frameSampleSize)
            {
                DData.lastVelSampleTime = 0;
                if (DData.velSamples.Count >= Options.maxGraphSamples)
                    _ = DData.velSamples.Dequeue();
                DData.velSamples.Enqueue(speed);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SampleWeapon(MutableString str, _Weapon wpn, float modifier, string name)
        {
            if (wpn == null) return;
            var finalDmg = wpn.baseDamage * modifier;
            var dmgType = DData.dmgTypes[(int)wpn.damageType];
            if (wpn is _ChargeWeapon chargWpn && chargedPower(chargWpn) > 0.999999)
            {
                finalDmg *= 1.5f;
                modifier *= 1.5f;
            }
	        str.Append(name).Append(finalDmg,2).Append(" (").Append(wpn.baseDamage, 2).Append("*").Append(modifier)
                   .Append(")").Append(" [").Append(dmgType).Append("]");
        }

        private void SpawnAtCursor()
        {
            RaycastHit hit;
            Ray ray = Cache.mainCam.ScreenPointToRay(Input.mousePosition);
            if (GameRoom.currentRoom == null)
            {
                if (Physics.Raycast(ray, out hit, 5000f, Globals.instance.solidLayers))
                    PlayerGlobal.instance.SetPosition(hit.point, false, false);
            }
            else
            {
                var c = Physics.RaycastNonAlloc(ray, Cache.hitsCache, 5000f, Globals.instance.solidLayers);
                Vector3 pos = Vector3.zero;
		        bool found = false;
		        float maxDistance = 999999f;
		        for (int i = 0; i < c; i++)
		        {
			        if (Cache.hitsCache[i].collider != null && GameRoom.currentRoom.PointInsideRoom(Cache.hitsCache[i].point))
			        {
				        found = true;
				        if (Cache.hitsCache[i].distance < maxDistance)
				        {
					        pos = Cache.hitsCache[i].point;
					        maxDistance = Cache.hitsCache[i].distance;
				        }
			        }
		        }
		        if (found)
		        {
			        GameRoom.currentRoom.debugForceExitRoom();
			        PlayerGlobal.instance.SetPosition(pos, false, false);
		        }
            }
        }

        private void OnEnable() => SceneManager.activeSceneChanged += OnSceneActivated;

        private void OnDisable() => SceneManager.activeSceneChanged -= OnSceneActivated;

        private void OnSceneActivated(Scene from, Scene to)
        {
            if (DData != null)
            {
                DData.lastActiveScene = from.name;
                DData.curActiveScene = to.name;
                ClearAllCache();
                if (to.name == "TitleScreen")
                {
                    Cache.mainCam = null;
                    Cache.cineBrain = null;
                    Cache.lineRenderer = null;
                    Cache.virtCam = null;
                }
            }
        }
    }
}