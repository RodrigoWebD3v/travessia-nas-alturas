using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Monta o jogo a partir dos assets gerados no Blender: materiais, prefabs, HUD e as duas fases.
/// Menu: Travessia > Construir Tudo. Pode ser executado de novo para regerar as cenas.
/// </summary>
public static class TravessiaBuilder
{
    const string ModelsDir = "Assets/Art/Models";
    const string TexDir = "Assets/Art/Textures";
    const string MatDir = "Assets/Art/Materials";
    const string AudioDir = "Assets/Audio";
    const string PrefabDir = "Assets/Prefabs";
    const string SceneDir = "Assets/Scenes";

    static readonly string[] Models =
        { "Equilibrista", "Bastao", "Moeda", "Coracao", "Bota", "Arvore", "Torre", "Relogio", "Gosma", "Espinhos", "Bandeira", "Passaro" };

    static Font font;

    [MenuItem("Travessia/Construir Tudo")]
    public static void BuildAll()
    {
        EnsureFolders();
        ConfigureImporters();
        CreateMaterials();
        CreatePrefabs();
        string fase1 = BuildPhase1();
        string fase2 = BuildPhase2();
        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(fase1, true),
            new EditorBuildSettingsScene(fase2, true)
        };
        PlayerSettings.productName = "Travessia nas Alturas";
        PlayerSettings.runInBackground = true;
        AssetDatabase.SaveAssets();
        EditorSceneManager.OpenScene(fase1);
        Debug.Log("Travessia nas Alturas: construção concluída.");
    }

    // ================================================================ assets

    static void EnsureFolders()
    {
        foreach (var path in new[] { MatDir, PrefabDir, SceneDir })
        {
            if (AssetDatabase.IsValidFolder(path)) continue;
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }
    }

    static void ConfigureImporters()
    {
        foreach (var name in Models)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath($"{ModelsDir}/{name}.fbx");
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importCameras = false;
            importer.importLights = false;
            if (name == "Equilibrista")
                ConfigureCharacter(importer);
            else
            {
                importer.importAnimation = false;
                importer.animationType = ModelImporterAnimationType.None;
            }
            importer.SaveAndReimport();
        }

        foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { AudioDir }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var importer = (AudioImporter)AssetImporter.GetAtPath(path);
            var settings = importer.defaultSampleSettings;
            bool music = System.IO.Path.GetFileName(path).StartsWith("Musica");
            settings.loadType = music ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = 0.7f;
            importer.defaultSampleSettings = settings;
            importer.SaveAndReimport();
        }
    }

    // Clipes feitos no Blender (gerar_personagem.py) e se repetem em loop.
    static readonly string[] LoopClips = { "Idle", "Walk", "RopeIdle", "RopeWalk", "Fall", "Crouch" };

    /// <summary>Rig genérico com as animações do Blender; Jump toca uma vez e segura a pose.</summary>
    static void ConfigureCharacter(ModelImporter importer)
    {
        importer.animationType = ModelImporterAnimationType.Generic;
        importer.importAnimation = true;
        importer.bakeAxisConversion = true;
        // Sem compressão: mantém todas as curvas, inclusive as constantes dos ossos parados.
        importer.animationCompression = ModelImporterAnimationCompression.Off;
        // Os takes só aparecem depois de importar como Generic; por isso reimporta antes de configurar os clipes.
        importer.SaveAndReimport();
        var clips = importer.defaultClipAnimations;
        foreach (var clip in clips)
        {
            clip.name = clip.takeName;
            clip.loopTime = System.Array.IndexOf(LoopClips, clip.takeName) >= 0;
        }
        importer.clipAnimations = clips;
    }

    static AnimationClip CharacterClip(string name)
    {
        foreach (var asset in AssetDatabase.LoadAllAssetsAtPath($"{ModelsDir}/Equilibrista.fbx"))
            if (asset is AnimationClip clip && clip.name == name) return clip;
        Debug.LogError("Clipe não encontrado: " + name);
        return null;
    }

    /// <summary>
    /// Um estado por animação. O PlayerController escolhe o estado por código (CrossFade),
    /// o que deixa as regras de transição explícitas no script.
    /// </summary>
    static UnityEditor.Animations.AnimatorController CreateAnimator()
    {
        const string path = "Assets/Art/Animacao/Equilibrista.controller";
        if (!AssetDatabase.IsValidFolder("Assets/Art/Animacao")) AssetDatabase.CreateFolder("Assets/Art", "Animacao");
        AssetDatabase.DeleteAsset(path);
        var controller = UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPath(path);
        var machine = controller.layers[0].stateMachine;
        foreach (var name in new[] { "Idle", "Walk", "RopeIdle", "RopeWalk", "Jump", "Fall", "Crouch" })
        {
            var state = machine.AddState(name);
            state.motion = CharacterClip(name);
            if (name == "Idle") machine.defaultState = state;
        }
        return controller;
    }

    static Transform FindDeep(Transform t, string name)
    {
        if (t.name == name) return t;
        foreach (Transform child in t)
        {
            var found = FindDeep(child, name);
            if (found != null) return found;
        }
        return null;
    }

    static Material Lit(string name, Texture2D texture, Color color, float smoothness = 0.15f)
    {
        string path = $"{MatDir}/{name}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.SetTexture("_BaseMap", texture);
        mat.SetColor("_BaseColor", color);
        mat.SetFloat("_Smoothness", smoothness);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static Texture2D Tex(string name) => AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexDir}/{name}.png");
    static Material Mat(string name) => AssetDatabase.LoadAssetAtPath<Material>($"{MatDir}/{name}.mat");
    static GameObject Model(string name) => AssetDatabase.LoadAssetAtPath<GameObject>($"{ModelsDir}/{name}.fbx");
    static GameObject Prefab(string name) => AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabDir}/{name}.prefab");
    static AudioClip Clip(string name) => AssetDatabase.LoadAssetAtPath<AudioClip>($"{AudioDir}/{name}.wav");

    static void CreateMaterials()
    {
        foreach (var name in Models)
            Lit("M_" + name, Tex("T_" + name), Color.white, name == "Moeda" ? 0.6f : 0.15f);
        Lit("M_Grama", Tex("T_Grama"), Color.white, 0.05f);
        Lit("M_Terra", Tex("T_Terra"), Color.white, 0.05f);
        Lit("M_Pedra", Tex("T_Pedra"), Color.white, 0.1f);
        Lit("M_Corda", null, new Color(0.55f, 0.36f, 0.2f), 0.1f);
        Lit("M_Madeira", null, new Color(0.55f, 0.35f, 0.21f), 0.1f);
        var cloud = Lit("M_Nuvem", null, Color.white, 0.0f);
        cloud.EnableKeyword("_EMISSION");
        cloud.SetColor("_EmissionColor", new Color(0.35f, 0.38f, 0.42f));
        cloud.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
    }

    // ================================================================ prefabs

    static GameObject InstModel(string model, Transform parent, Vector3 localPos, float scale = 1f, float yaw = 0f)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(Model(model));
        go.name = model;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        go.transform.localScale = Vector3.one * scale;
        var mat = Mat("M_" + model);
        foreach (var r in go.GetComponentsInChildren<Renderer>())
        {
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.On;
        }
        return go;
    }

    static void SavePrefab(GameObject root, string name)
    {
        PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabDir}/{name}.prefab");
        Object.DestroyImmediate(root);
    }

    static void CreatePrefabs()
    {
        // ---------- Equilibrista
        var player = new GameObject("Equilibrista");
        player.tag = "Player";
        var cc = player.AddComponent<CharacterController>();
        cc.center = new Vector3(0f, 0.8f, 0f);
        cc.height = 1.6f;
        cc.radius = 0.3f;
        cc.stepOffset = 0.3f;
        cc.skinWidth = 0.04f;
        cc.slopeLimit = 50f;
        player.AddComponent<PlayerStats>();
        player.AddComponent<BalanceController>();

        var visual = new GameObject("Visual").transform;
        visual.SetParent(player.transform, false);
        var tilt = new GameObject("PivoInclinacao").transform;
        tilt.SetParent(visual, false);
        // O FBX do Blender olha para -Z; girando 180° o personagem nasce de frente para o percurso (+Z).
        var model = InstModel("Equilibrista", tilt, Vector3.zero, 1f, 180f);
        var animator = model.GetComponent<Animator>();
        if (animator == null) animator = model.AddComponent<Animator>();
        animator.runtimeAnimatorController = CreateAnimator();
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        // A vara gira em torno do ponto entre as mãos (pivô filho do peito); os braços seguem por IK.
        var rig = model.AddComponent<PoleRig>();
        rig.shoulderRig = FindDeep(model.transform, "ShoulderRig");
        rig.upperArmL = FindDeep(model.transform, "UpperArm.L");
        rig.lowerArmL = FindDeep(model.transform, "LowerArm.L");
        rig.handL = FindDeep(model.transform, "Hand.L");
        rig.upperArmR = FindDeep(model.transform, "UpperArm.R");
        rig.lowerArmR = FindDeep(model.transform, "LowerArm.R");
        rig.handR = FindDeep(model.transform, "Hand.R");
        rig.forwardReference = tilt;

        var pivot = new GameObject("PivoVara").transform;
        pivot.SetParent(FindDeep(model.transform, "Chest"), false);
        pivot.position = (rig.handL.position + rig.handR.position) * 0.5f;
        pivot.rotation = visual.rotation;
        var pole = InstModel("Bastao", pivot, Vector3.zero);
        pole.transform.localRotation = Quaternion.identity;
        rig.pivot = pivot;
        rig.gripL = new GameObject("PegadaEsq").transform;
        rig.gripL.SetParent(pivot, false);
        rig.gripL.position = rig.handL.position;
        rig.gripR = new GameObject("PegadaDir").transform;
        rig.gripR.SetParent(pivot, false);
        rig.gripR.position = rig.handR.position;
        var poleRig = rig;

        var sensor = new GameObject("SensorColeta");
        sensor.transform.SetParent(player.transform, false);
        var capsule = sensor.AddComponent<CapsuleCollider>();
        capsule.isTrigger = true;
        capsule.center = new Vector3(0f, 0.8f, 0f);
        capsule.height = 1.6f;
        capsule.radius = 0.35f;
        var body = sensor.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;

        var pc = player.AddComponent<PlayerController>();
        pc.visualRoot = visual;
        pc.tiltPivot = tilt;
        pc.poleRig = poleRig;
        pc.animator = animator;
        pc.pickupSensor = capsule;
        SavePrefab(player, "Equilibrista");

        // ---------- coletáveis
        MakeCollectible("Moeda", CollectibleType.Coin, "Moeda", 1f, Vector3.zero, 0.5f);
        MakeCollectible("Coracao", CollectibleType.Heart, "Coracao", 1.1f, Vector3.zero, 0.5f);
        MakeCollectible("Bota", CollectibleType.Boot, "Bota", 1.1f, new Vector3(0f, -0.3f, 0f), 0.5f);
        MakeCollectible("Relogio", CollectibleType.Clock, "Relogio", 1.1f, Vector3.zero, 0.5f);
        MakeCollectible("Gosma", CollectibleType.Slime, "Gosma", 1.2f, Vector3.zero, 0.55f, groundItem: true);

        var spikes = new GameObject("Espinhos");
        var box = spikes.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.center = new Vector3(0f, 0.35f, 0f);
        box.size = new Vector3(1.1f, 0.7f, 1.1f);
        var sc = spikes.AddComponent<Collectible>();
        sc.type = CollectibleType.Spikes;
        sc.visual = InstModel("Espinhos", spikes.transform, Vector3.zero).transform;
        SavePrefab(spikes, "Espinhos");

        // ---------- bandeira de chegada
        var flag = new GameObject("BandeiraChegada");
        var flagBox = flag.AddComponent<BoxCollider>();
        flagBox.isTrigger = true;
        flagBox.center = new Vector3(0f, 1.75f, 0f);
        flagBox.size = new Vector3(2f, 3.5f, 2f);
        flag.AddComponent<LevelExit>();
        InstModel("Bandeira", flag.transform, Vector3.zero, 1f, 180f);
        SavePrefab(flag, "BandeiraChegada");

        // ---------- árvore (decoração A) e torre (decoração B)
        var tree = new GameObject("Arvore");
        InstModel("Arvore", tree.transform, Vector3.zero);
        var trunk = tree.AddComponent<CapsuleCollider>();
        trunk.center = new Vector3(0f, 1f, 0f);
        trunk.height = 2f;
        trunk.radius = 0.25f;
        SavePrefab(tree, "Arvore");

        // ---------- pássaro (perigo das cordas)
        var bird = new GameObject("Passaro");
        var birdCollider = bird.AddComponent<SphereCollider>();
        birdCollider.isTrigger = true;
        birdCollider.radius = 0.3f;
        var birdBody = bird.AddComponent<Rigidbody>();
        birdBody.isKinematic = true;
        birdBody.useGravity = false;
        var hazard = bird.AddComponent<BirdHazard>();
        var birdModel = InstModel("Passaro", bird.transform, Vector3.zero, 1.3f);
        hazard.wingL = FindDeep(birdModel.transform, "Passaro_AsaEsq");
        hazard.wingR = FindDeep(birdModel.transform, "Passaro_AsaDir");
        SavePrefab(bird, "Passaro");

        var tower = new GameObject("Torre");
        InstModel("Torre", tower.transform, Vector3.zero);
        SavePrefab(tower, "Torre");
    }

    static void MakeCollectible(string name, CollectibleType type, string model, float scale, Vector3 visualOffset,
                                float radius, bool groundItem = false)
    {
        var root = new GameObject(name);
        var sphere = root.AddComponent<SphereCollider>();
        sphere.isTrigger = true;
        sphere.radius = radius;
        sphere.center = groundItem ? new Vector3(0f, 0.3f, 0f) : Vector3.zero;
        var c = root.AddComponent<Collectible>();
        c.type = type;
        var pivot = new GameObject("Visual").transform;
        pivot.SetParent(root.transform, false);
        pivot.localPosition = visualOffset;
        InstModel(model, pivot, Vector3.zero, scale, 180f);
        c.visual = pivot;
        if (groundItem)
        {
            c.spinSpeed = 25f;
            c.bobAmplitude = 0.03f;
        }
        SavePrefab(root, name);
    }

    // ================================================================ cena

    class LevelContext
    {
        public Transform root;
        public Transform items;
        public Transform decor;
        public bool stoneBase;
    }

    static GameObject Place(string prefab, Transform parent, Vector3 position, float scale = 1f, float yaw = 0f)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(Prefab(prefab));
        go.transform.SetParent(parent, true);
        go.transform.position = position;
        go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        go.transform.localScale = Vector3.one * scale;
        return go;
    }

    static Transform Group(string name, Transform parent = null)
    {
        var go = new GameObject(name);
        if (parent != null) go.transform.SetParent(parent, false);
        return go.transform;
    }

    static void Platform(LevelContext ctx, string name, Vector3 topCenter, Vector2 size, float thickness = 1.4f)
    {
        var root = Group(name, ctx.root);
        root.position = topCenter;

        var grass = GameObject.CreatePrimitive(PrimitiveType.Cube);
        grass.name = "Grama";
        grass.transform.SetParent(root, false);
        grass.transform.localPosition = new Vector3(0f, -0.15f, 0f);
        grass.transform.localScale = new Vector3(size.x, 0.3f, size.y);
        grass.GetComponent<Renderer>().sharedMaterial = Mat("M_Grama");
        grass.AddComponent<AutoTiling>().useXZ = true;

        var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.name = ctx.stoneBase ? "Pedra" : "Terra";
        body.transform.SetParent(root, false);
        body.transform.localPosition = new Vector3(0f, -0.3f - thickness * 0.5f, 0f);
        body.transform.localScale = new Vector3(size.x - 0.15f, thickness, size.y - 0.15f);
        body.GetComponent<Renderer>().sharedMaterial = Mat(ctx.stoneBase ? "M_Pedra" : "M_Terra");
        var tiling = body.AddComponent<AutoTiling>();
        tiling.useXZ = false;
        tiling.unitsPerTile = ctx.stoneBase ? 2.5f : 2f;

        // ponta inferior para a ilha parecer flutuante
        var tip = GameObject.CreatePrimitive(PrimitiveType.Cube);
        tip.name = "Base";
        tip.transform.SetParent(root, false);
        tip.transform.localPosition = new Vector3(0f, -0.3f - thickness - 0.35f, 0f);
        tip.transform.localScale = new Vector3(size.x * 0.6f, 0.7f, size.y * 0.6f);
        tip.GetComponent<Renderer>().sharedMaterial = body.GetComponent<Renderer>().sharedMaterial;
        tip.AddComponent<AutoTiling>().useXZ = false;
        Object.DestroyImmediate(tip.GetComponent<Collider>());
    }

    static RopePath Rope(LevelContext ctx, string name, Vector3 a, Vector3 b, float sag, params float[] coinTs)
    {
        var root = Group(name, ctx.root);
        root.position = a;
        var start = Group("InicioCorda", root);
        start.position = a;
        var end = Group("FimCorda", root);
        end.position = b;

        var line = root.gameObject.AddComponent<LineRenderer>();
        line.sharedMaterial = Mat("M_Corda");
        line.widthMultiplier = 0.09f;
        line.numCapVertices = 2;
        line.numCornerVertices = 2;
        line.useWorldSpace = true;
        line.shadowCastingMode = ShadowCastingMode.On;

        var rope = root.gameObject.AddComponent<RopePath>();
        rope.startAnchor = start;
        rope.endAnchor = end;
        rope.sag = sag;
        rope.line = line;
        rope.UpdateLine();

        Vector3 forward = (b - a);
        forward.y = 0f;
        forward.Normalize();
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        foreach (var anchor in new[] { a, b })
        {
            foreach (float side in new[] { -1f, 1f })
            {
                var post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                post.name = "Poste";
                post.transform.SetParent(root, true);
                post.transform.position = anchor + right * side * 0.45f + Vector3.up * 0.45f;
                post.transform.localScale = new Vector3(0.14f, 0.45f, 0.14f);
                post.GetComponent<Renderer>().sharedMaterial = Mat("M_Madeira");
                Object.DestroyImmediate(post.GetComponent<Collider>());
            }
        }

        foreach (float t in coinTs)
        {
            Vector3 p = Vector3.Lerp(a, b, t);
            p.y -= sag * 4f * t * (1f - t);
            Place("Moeda", ctx.items, p + Vector3.up * 0.95f);
        }
        return rope;
    }

    static void BirdSpawnerFor(LevelContext ctx, float minInterval, float maxInterval, float speed, params RopePath[] ropes)
    {
        var spawner = Group("Passaros", ctx.root).gameObject.AddComponent<BirdSpawner>();
        spawner.birdPrefab = Prefab("Passaro").GetComponent<BirdHazard>();
        spawner.ropes = ropes;
        spawner.minInterval = minInterval;
        spawner.maxInterval = maxInterval;
        spawner.birdSpeed = speed;
    }

    static void CheckpointAt(LevelContext ctx, Vector3 position, float platformWidth)
    {
        var root = Group("Checkpoint", ctx.root);
        root.position = position;
        var box = root.gameObject.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.center = new Vector3(0f, 1.25f, 0f);
        box.size = new Vector3(platformWidth, 2.5f, 2f);
        var cp = root.gameObject.AddComponent<Checkpoint>();
        var flag = InstModel("Bandeira", root, new Vector3(-platformWidth * 0.5f + 0.55f, 0f, 0f), 0.55f, 180f);
        cp.flagRenderer = flag.GetComponentInChildren<Renderer>();
        cp.spawnPoint = root;
    }

    static void Cloud(Transform parent, Vector3 position, float scale)
    {
        var root = Group("Nuvem", parent);
        root.position = position;
        var rng = new System.Random((int)(position.x * 13 + position.z * 7 + position.y * 3));
        int puffs = 4 + rng.Next(3);
        for (int i = 0; i < puffs; i++)
        {
            var puff = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            puff.name = "Bolha";
            puff.transform.SetParent(root, false);
            float r = (float)(0.8 + rng.NextDouble() * 0.7);
            puff.transform.localPosition = new Vector3((i - puffs / 2f) * 1.1f, (float)rng.NextDouble() * 0.4f, (float)(rng.NextDouble() - 0.5) * 1.2f);
            puff.transform.localScale = new Vector3(r * 1.6f, r, r * 1.3f);
            puff.GetComponent<Renderer>().sharedMaterial = Mat("M_Nuvem");
            puff.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            Object.DestroyImmediate(puff.GetComponent<Collider>());
        }
        root.localScale = Vector3.one * scale;
        var floater = root.gameObject.AddComponent<Floater>();
        floater.amplitude = 1f + scale * 0.5f;
        floater.speed = 0.15f + (float)rng.NextDouble() * 0.15f;
    }

    static void CloudField(LevelContext ctx, float zMin, float zMax, int seed)
    {
        var rng = new System.Random(seed);
        var clouds = Group("Nuvens", ctx.decor);
        for (int i = 0; i < 26; i++)
        {
            float x = (float)(rng.NextDouble() * 60 - 30);
            float z = (float)(zMin + rng.NextDouble() * (zMax - zMin));
            float y = i % 3 == 0 ? (float)(6 + rng.NextDouble() * 6) : (float)(-12 - rng.NextDouble() * 8);
            if (Mathf.Abs(x) < 6 && y > 0) x += 14f * Mathf.Sign(x == 0 ? 1 : x);
            Cloud(clouds, new Vector3(x, y, z), (float)(1.2 + rng.NextDouble() * 2.0));
        }
        // camada de nuvens abaixo do percurso
        for (int i = 0; i < 14; i++)
            Cloud(clouds, new Vector3((float)(rng.NextDouble() * 24 - 12), -10f - (float)rng.NextDouble() * 3,
                                      zMin + (zMax - zMin) * i / 13f), 3f);
    }

    static (Scene scene, LevelContext ctx) NewLevel(string title, Color fogColor)
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var light = new GameObject("Sol").AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.25f;
        light.color = new Color(1f, 0.96f, 0.88f);
        light.shadows = LightShadows.Soft;
        light.transform.rotation = Quaternion.Euler(48f, -35f, 0f);

        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.75f, 0.88f, 1f);
        RenderSettings.ambientEquatorColor = new Color(0.62f, 0.75f, 0.68f);
        RenderSettings.ambientGroundColor = new Color(0.42f, 0.5f, 0.45f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = fogColor;
        RenderSettings.fogStartDistance = 40f;
        RenderSettings.fogEndDistance = 120f;
        RenderSettings.skybox = null;

        var ctx = new LevelContext
        {
            root = Group("Fase"),
        };
        ctx.items = Group("Itens", ctx.root);
        ctx.decor = Group("Decoracao", ctx.root);
        return (scene, ctx);
    }

    static GameManager SetupGameplay(LevelContext ctx, Vector3 spawn, string title, string nextScene, AudioClip music)
    {
        var player = Place("Equilibrista", null, spawn).GetComponent<PlayerController>();

        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color32(0x8E, 0xD6, 0xFF, 0xFF);
        cam.fieldOfView = 55f;
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 250f;
        camGo.AddComponent<AudioListener>();
        var follow = camGo.AddComponent<CameraFollow>();
        follow.target = player.transform;
        camGo.transform.position = spawn + follow.offset;
        camGo.transform.LookAt(spawn + follow.lookOffset);

        var audioGo = new GameObject("AudioManager");
        var audio = audioGo.AddComponent<AudioManager>();
        audio.musicSource = audioGo.AddComponent<AudioSource>();
        audio.musicSource.playOnAwake = false;
        audio.musicSource.loop = true;
        audio.musicSource.volume = 0.45f;
        audio.sfxSource = audioGo.AddComponent<AudioSource>();
        audio.sfxSource.playOnAwake = false;
        audio.sfxSource.volume = 0.85f;
        audio.phaseMusic = music;
        audio.gameOverMusic = Clip("Musica_GameOver");
        audio.coin = Clip("Sfx_Moeda");
        audio.heart = Clip("Sfx_Coracao");
        audio.boot = Clip("Sfx_Bota");
        audio.slime = Clip("Sfx_Gosma");
        audio.spikes = Clip("Sfx_Espinhos");
        audio.clock = Clip("Sfx_Relogio");
        audio.fall = Clip("Sfx_Queda");
        audio.checkpoint = Clip("Sfx_Checkpoint");
        audio.flag = Clip("Sfx_Bandeira");
        audio.jump = Clip("Sfx_Pulo");
        audio.denied = Clip("Sfx_Negado");
        audio.bird = Clip("Sfx_Passaro");
        audio.birdHit = Clip("Sfx_Bicada");

        var hud = BuildHUD();

        var gm = new GameObject("GameManager").AddComponent<GameManager>();
        gm.phaseTitle = title;
        gm.nextScene = nextScene;
        gm.player = player;
        gm.stats = player.Stats != null ? player.Stats : player.GetComponent<PlayerStats>();
        gm.hud = hud;
        gm.audioManager = audio;
        return gm;
    }

    // ================================================================ fases

    static string BuildPhase1()
    {
        var (scene, ctx) = NewLevel("Jardim Suspenso", new Color(0.66f, 0.86f, 1f));
        var gm = SetupGameplay(ctx, new Vector3(0f, 0.1f, -2f), "Fase 1 - Jardim Suspenso",
                               GameSession.Phases[1], Clip("Musica_Fase1"));
        gm.killHeight = -7f;

        // Plataforma inicial e primeira corda curta
        Platform(ctx, "P0_Inicio", new Vector3(0f, 0f, 0f), new Vector2(8f, 8f));
        Place("Moeda", ctx.items, new Vector3(-2f, 0.9f, 1.5f));
        Place("Moeda", ctx.items, new Vector3(2f, 0.9f, 1.5f));
        Place("Arvore", ctx.decor, new Vector3(-3f, 0f, -3f), 1f, 20f);
        Place("Arvore", ctx.decor, new Vector3(3.1f, 0f, -2.7f), 0.85f, 110f);

        Rope(ctx, "Corda1", new Vector3(0f, 0f, 4f), new Vector3(0f, 0f, 11f), 0.3f, 0.3f, 0.55f, 0.8f);

        // Plataforma com coração e checkpoint
        Platform(ctx, "P1_Checkpoint", new Vector3(0f, 0f, 14f), new Vector2(6f, 6f));
        CheckpointAt(ctx, new Vector3(0f, 0f, 12.6f), 6f);
        Place("Coracao", ctx.items, new Vector3(2.1f, 0.9f, 14.2f));
        Place("Moeda", ctx.items, new Vector3(0f, 0.9f, 15.6f));
        Place("Arvore", ctx.decor, new Vector3(2.2f, 0f, 16.3f), 0.8f, 200f);

        // Pequeno salto com moeda no arco
        Place("Moeda", ctx.items, new Vector3(0f, 1.9f, 17.75f));
        Platform(ctx, "P2_Salto", new Vector3(0f, 0.4f, 20.5f), new Vector2(4f, 4f));
        Place("Moeda", ctx.items, new Vector3(0f, 1.3f, 21.2f));

        var corda2 = Rope(ctx, "Corda2", new Vector3(0f, 0.4f, 22.5f), new Vector3(0f, 0.4f, 32.5f), 0.35f, 0.2f, 0.4f, 0.6f, 0.8f);
        // Fase 1: pássaros só na corda longa, com intervalos generosos (ensino).
        BirdSpawnerFor(ctx, 4f, 5.5f, 5.5f, corda2);

        // Trecho final com bota opcional, relógio, perigos e bandeira
        Platform(ctx, "P3_Final", new Vector3(0f, 0.4f, 36.5f), new Vector2(8f, 8f));
        CheckpointAt(ctx, new Vector3(0f, 0.4f, 33.8f), 8f);
        Place("Relogio", ctx.items, new Vector3(1.8f, 1.3f, 34.4f));
        Place("Moeda", ctx.items, new Vector3(-1.5f, 1.3f, 35.8f));
        Place("Moeda", ctx.items, new Vector3(1.5f, 1.3f, 35.8f));
        Place("Moeda", ctx.items, new Vector3(0f, 1.3f, 37.8f));
        Place("Espinhos", ctx.items, new Vector3(-2.6f, 0.4f, 37.6f));
        Place("Gosma", ctx.items, new Vector3(2.6f, 0.4f, 37.4f));
        Place("Bota", ctx.items, new Vector3(3.1f, 1.3f, 39.4f));
        Place("BandeiraChegada", ctx.root, new Vector3(0f, 0.4f, 39.6f));
        Place("Arvore", ctx.decor, new Vector3(-3.2f, 0.4f, 39.9f), 0.9f, 60f);
        Place("Arvore", ctx.decor, new Vector3(3.3f, 0.4f, 33.2f), 0.75f, 300f);

        // Torres distantes (decoração B) e nuvens
        Place("Torre", ctx.decor, new Vector3(-14f, -12f, 10f), 2f);
        Place("Torre", ctx.decor, new Vector3(15f, -14f, 24f), 2.5f, 90f);
        Place("Torre", ctx.decor, new Vector3(-17f, -13f, 40f), 2.2f, 200f);
        Place("Torre", ctx.decor, new Vector3(13f, -10f, 50f), 1.8f, 45f);
        Place("Torre", ctx.decor, new Vector3(-4f, -16f, 62f), 3f);
        CloudField(ctx, -10f, 70f, 11);

        string path = $"{SceneDir}/{GameSession.Phases[0]}.unity";
        EditorSceneManager.SaveScene(scene, path);
        return path;
    }

    static string BuildPhase2()
    {
        var (scene, ctx) = NewLevel("Torres nas Nuvens", new Color(0.7f, 0.82f, 0.95f));
        ctx.stoneBase = true;
        var gm = SetupGameplay(ctx, new Vector3(0f, 0.1f, -1f), "Fase 2 - Torres nas Nuvens",
                               "", Clip("Musica_Fase2"));
        gm.killHeight = -7f;

        Platform(ctx, "T0_Inicio", new Vector3(0f, 0f, 0f), new Vector2(5f, 5f));
        Place("Moeda", ctx.items, new Vector3(0f, 0.9f, 1.3f));

        var cordaA = Rope(ctx, "CordaA", new Vector3(0f, 0f, 2.5f), new Vector3(0f, 0f, 14.5f), 0.45f, 0.2f, 0.4f, 0.6f, 0.8f);

        // Plataforma com espinhos e gosma nas laterais (centro livre)
        Platform(ctx, "T1_Perigos", new Vector3(0f, 0f, 17f), new Vector2(5f, 5f));
        CheckpointAt(ctx, new Vector3(0f, 0f, 15.5f), 5f);
        Place("Espinhos", ctx.items, new Vector3(1.6f, 0f, 17.6f));
        Place("Gosma", ctx.items, new Vector3(-1.6f, 0f, 18.4f));
        Place("Moeda", ctx.items, new Vector3(0f, 0.9f, 17.4f));
        Place("Moeda", ctx.items, new Vector3(0f, 0.9f, 18.9f));

        // Salto para T2 e desvio opcional (T2b) com coração e relógio
        Place("Moeda", ctx.items, new Vector3(0f, 2f, 20f));
        Platform(ctx, "T2_Salto", new Vector3(0f, 0.5f, 22.5f), new Vector2(4f, 4f));
        Place("Moeda", ctx.items, new Vector3(0f, 1.4f, 23.2f));
        Platform(ctx, "T2b_Desvio", new Vector3(5f, 0.5f, 22.5f), new Vector2(3f, 3f));
        Place("Coracao", ctx.items, new Vector3(5f, 1.4f, 21.8f));
        Place("Relogio", ctx.items, new Vector3(5f, 1.4f, 23.3f));

        // Corda longa e ascendente
        var cordaB = Rope(ctx, "CordaB", new Vector3(0f, 0.5f, 24.5f), new Vector3(0f, 1.3f, 38.5f), 0.5f, 0.15f, 0.32f, 0.5f, 0.68f, 0.85f);

        Platform(ctx, "T3_Checkpoint", new Vector3(0f, 1.3f, 41f), new Vector2(5f, 5f));
        CheckpointAt(ctx, new Vector3(0f, 1.3f, 39.6f), 5f);
        Place("Bota", ctx.items, new Vector3(1.6f, 2.2f, 41.6f));
        Place("Espinhos", ctx.items, new Vector3(-1.6f, 1.3f, 42.4f));
        Place("Moeda", ctx.items, new Vector3(0f, 2.2f, 42.2f));

        // Travessia final
        var cordaC = Rope(ctx, "CordaC", new Vector3(0f, 1.3f, 43.5f), new Vector3(0f, 1.3f, 51.5f), 0.35f, 0.33f, 0.66f);
        // Fase 2: pássaros em todas as cordas, mais frequentes e mais rápidos.
        BirdSpawnerFor(ctx, 2.8f, 4f, 6.5f, cordaA, cordaB, cordaC);
        Platform(ctx, "T4_Final", new Vector3(0f, 1.3f, 54f), new Vector2(5f, 5f));
        Place("Moeda", ctx.items, new Vector3(-1.5f, 2.2f, 53f));
        Place("Moeda", ctx.items, new Vector3(1.5f, 2.2f, 53f));
        Place("BandeiraChegada", ctx.root, new Vector3(0f, 1.3f, 55.5f));

        // Torres que sobem das nuvens ao lado do percurso (decoração B)
        float[] zs = { 6f, 18f, 30f, 44f, 58f };
        for (int i = 0; i < zs.Length; i++)
        {
            float side = i % 2 == 0 ? -1f : 1f;
            Place("Torre", ctx.decor, new Vector3(side * 7.5f, -16f, zs[i]), 2.4f, i * 37f);
            Place("Torre", ctx.decor, new Vector3(-side * 13f, -20f, zs[i] + 6f), 2.8f, i * 53f);
        }
        Place("Torre", ctx.decor, new Vector3(0f, -18f, 70f), 3.5f);
        Place("Arvore", ctx.decor, new Vector3(5.8f, 0.5f, 21.7f), 0.6f, 30f);
        Place("Arvore", ctx.decor, new Vector3(-1.8f, 1.3f, 55.6f), 0.7f, 90f);
        CloudField(ctx, -10f, 75f, 23);

        string path = $"{SceneDir}/{GameSession.Phases[1]}.unity";
        EditorSceneManager.SaveScene(scene, path);
        return path;
    }

    // ================================================================ HUD

    static RectTransform Rect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
                              Vector2 position, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = position;
        rt.sizeDelta = size;
        return rt;
    }

    static Image Panel(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
                       Vector2 position, Vector2 size, Color color)
    {
        var rt = Rect(name, parent, anchorMin, anchorMax, pivot, position, size);
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        img.type = Image.Type.Sliced;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    static Text Label(string name, Transform parent, string text, int size, TextAnchor align, Vector2 anchorMin,
                      Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 box, Color color,
                      FontStyle style = FontStyle.Bold)
    {
        var rt = Rect(name, parent, anchorMin, anchorMax, pivot, position, box);
        var t = rt.gameObject.AddComponent<Text>();
        t.font = font;
        t.text = text;
        t.fontSize = size;
        t.fontStyle = style;
        t.alignment = align;
        t.color = color;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        var outline = rt.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.7f);
        outline.effectDistance = new Vector2(2f, -2f);
        return t;
    }

    static HUDController BuildHUD()
    {
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        var canvasGo = new GameObject("HUD");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        var hud = canvasGo.AddComponent<HUDController>();
        var root = canvasGo.transform;

        Vector2 tl = new Vector2(0f, 1f), tr = new Vector2(1f, 1f), tc = new Vector2(0.5f, 1f);
        Vector2 bc = new Vector2(0.5f, 0f), center = new Vector2(0.5f, 0.5f);

        // Pontos e velocidade (canto superior esquerdo)
        var left = Panel("PainelStatus", root, tl, tl, tl, new Vector2(24f, -24f), new Vector2(440f, 150f), new Color(0f, 0f, 0f, 0.35f));
        hud.pointsText = Label("Pontos", left.transform, "Pontos: 0 / 100", 38, TextAnchor.MiddleLeft, tl, tl, tl,
                               new Vector2(20f, -12f), new Vector2(400f, 44f), new Color(1f, 0.86f, 0.3f));
        hud.totalText = Label("Total", left.transform, "Total: 0", 26, TextAnchor.MiddleLeft, tl, tl, tl,
                              new Vector2(20f, -58f), new Vector2(400f, 34f), Color.white, FontStyle.Normal);
        hud.speedText = Label("Velocidade", left.transform, "Velocidade: 3.0", 30, TextAnchor.MiddleLeft, tl, tl, tl,
                              new Vector2(20f, -96f), new Vector2(400f, 40f), new Color(0.55f, 0.85f, 1f));

        // Tempo (topo central)
        var timePanel = Panel("PainelTempo", root, tc, tc, tc, new Vector2(0f, -20f), new Vector2(220f, 110f), new Color(0f, 0f, 0f, 0.35f));
        Label("TempoTitulo", timePanel.transform, "TEMPO", 22, TextAnchor.MiddleCenter, tc, tc, tc,
              new Vector2(0f, -8f), new Vector2(200f, 28f), new Color(1f, 1f, 1f, 0.8f));
        hud.timeText = Label("Tempo", timePanel.transform, "60", 60, TextAnchor.MiddleCenter, tc, tc, tc,
                             new Vector2(0f, -36f), new Vector2(200f, 66f), Color.white);

        // Vida com Slider (canto superior direito)
        var right = Panel("PainelVida", root, tr, tr, tr, new Vector2(-24f, -24f), new Vector2(460f, 110f), new Color(0f, 0f, 0f, 0.35f));
        Label("VidaTitulo", right.transform, "VIDA", 28, TextAnchor.MiddleLeft, tl, tl, tl,
              new Vector2(20f, -10f), new Vector2(200f, 36f), new Color(1f, 0.6f, 0.65f));
        hud.healthText = Label("VidaValor", right.transform, "100", 28, TextAnchor.MiddleRight, tr, tr, tr,
                               new Vector2(-20f, -10f), new Vector2(120f, 36f), Color.white);
        var res = new DefaultControls.Resources
        {
            standard = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"),
            background = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd"),
            knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd"),
        };
        var sliderGo = DefaultControls.CreateSlider(res);
        sliderGo.name = "VidaSlider";
        sliderGo.transform.SetParent(right.transform, false);
        var srt = (RectTransform)sliderGo.transform;
        srt.anchorMin = srt.anchorMax = srt.pivot = tl;
        srt.anchoredPosition = new Vector2(20f, -56f);
        srt.sizeDelta = new Vector2(420f, 34f);
        var slider = sliderGo.GetComponent<Slider>();
        Object.DestroyImmediate(slider.handleRect.parent.gameObject);
        slider.handleRect = null;
        slider.interactable = false;
        slider.transition = Selectable.Transition.None;
        slider.minValue = 0f;
        slider.maxValue = 100f;
        slider.value = 100f;
        var fillArea = (RectTransform)slider.fillRect.parent;
        fillArea.offsetMin = new Vector2(4f, 4f);
        fillArea.offsetMax = new Vector2(-4f, -4f);
        fillArea.anchorMin = Vector2.zero;
        fillArea.anchorMax = Vector2.one;
        slider.fillRect.offsetMin = Vector2.zero;
        slider.fillRect.offsetMax = Vector2.zero;
        sliderGo.transform.Find("Background").GetComponent<Image>().color = new Color(0.15f, 0.1f, 0.1f, 0.9f);
        hud.healthSlider = slider;
        hud.healthFill = slider.fillRect.GetComponent<Image>();
        hud.healthFill.color = new Color(0.35f, 0.85f, 0.35f);

        // Indicador de equilíbrio (embaixo, só aparece na corda)
        var balance = Rect("Equilibrio", root, bc, bc, bc, new Vector2(0f, 150f), new Vector2(560f, 90f));
        hud.balanceRoot = balance.gameObject;
        Label("EquilibrioTitulo", balance, "EQUILÍBRIO", 24, TextAnchor.MiddleCenter, tc, tc, tc,
              new Vector2(0f, 0f), new Vector2(300f, 30f), Color.white);
        var bar = Panel("Barra", balance, bc, bc, bc, new Vector2(0f, 26f), new Vector2(520f, 30f), new Color(0.1f, 0.1f, 0.1f, 0.75f));
        hud.balanceBar = bar.rectTransform;
        var safe = Panel("ZonaSegura", bar.transform, new Vector2(0.3f, 0f), new Vector2(0.7f, 1f), center, Vector2.zero, Vector2.zero, new Color(0.35f, 0.85f, 0.4f, 0.55f));
        var dangerL = Panel("PerigoEsq", bar.transform, new Vector2(0f, 0f), new Vector2(0.12f, 1f), center, Vector2.zero, Vector2.zero, new Color(0.9f, 0.3f, 0.3f, 0.8f));
        var dangerR = Panel("PerigoDir", bar.transform, new Vector2(0.88f, 0f), new Vector2(1f, 1f), center, Vector2.zero, Vector2.zero, new Color(0.9f, 0.3f, 0.3f, 0.8f));
        var marker = Panel("Inclinacao", bar.transform, center, center, center, Vector2.zero, new Vector2(12f, 52f), Color.white);
        hud.tiltMarker = marker.rectTransform;
        hud.tiltMarkerImage = marker;
        var poleMarker = Panel("Vara", bar.transform, center, center, center, new Vector2(0f, -34f), new Vector2(150f, 8f), new Color(0.95f, 0.55f, 0.16f));
        hud.poleMarker = poleMarker.rectTransform;
        Label("TeclaA", balance, "A", 28, TextAnchor.MiddleCenter, bc, bc, bc, new Vector2(-290f, 20f), new Vector2(40f, 40f), Color.white);
        Label("TeclaD", balance, "D", 28, TextAnchor.MiddleCenter, bc, bc, bc, new Vector2(290f, 20f), new Vector2(40f, 40f), Color.white);
        _ = safe; _ = dangerL; _ = dangerR;

        // Aviso de pássaro (pisca do lado de onde ele vem)
        hud.birdWarning = Label("AvisoPassaro", root, "PÁSSARO À FRENTE!  Ctrl", 46, TextAnchor.MiddleCenter, center, center, center,
                                new Vector2(0f, 120f), new Vector2(900f, 70f), new Color(1f, 0.45f, 0.35f));

        // Mensagens
        hud.messageText = Label("Mensagem", root, "", 54, TextAnchor.MiddleCenter, center, center, center,
                                new Vector2(0f, 200f), new Vector2(1400f, 160f), Color.white);
        hud.toastText = Label("Aviso", root, "", 40, TextAnchor.MiddleCenter, center, center, center,
                              new Vector2(0f, 60f), new Vector2(600f, 60f), Color.white);
        hud.hintText = Label("Dica", root, "", 30, TextAnchor.MiddleCenter, bc, bc, bc,
                             new Vector2(0f, 40f), new Vector2(1600f, 50f), new Color(1f, 1f, 0.85f), FontStyle.Normal);

        // Game Over
        var gameOver = Panel("GameOver", root, Vector2.zero, Vector2.one, center, Vector2.zero, Vector2.zero, Color.black);
        gameOver.sprite = null;
        Label("GameOverTitulo", gameOver.transform, "GAME OVER", 140, TextAnchor.MiddleCenter, center, center, center,
              new Vector2(0f, 60f), new Vector2(1400f, 180f), new Color(1f, 0.3f, 0.3f));
        Label("GameOverEnter", gameOver.transform, "Aperte Enter para continuar", 44, TextAnchor.MiddleCenter, center, center, center,
              new Vector2(0f, -90f), new Vector2(1400f, 70f), Color.white, FontStyle.Normal);
        hud.gameOverPanel = gameOver.gameObject;

        // Vitória
        var victory = Panel("Vitoria", root, Vector2.zero, Vector2.one, center, Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0.85f));
        victory.sprite = null;
        Label("VitoriaTitulo", victory.transform, "VITÓRIA!", 130, TextAnchor.MiddleCenter, center, center, center,
              new Vector2(0f, 140f), new Vector2(1400f, 170f), new Color(1f, 0.85f, 0.3f));
        hud.victoryText = Label("VitoriaPontos", victory.transform, "", 46, TextAnchor.MiddleCenter, center, center, center,
                                new Vector2(0f, -10f), new Vector2(1400f, 130f), Color.white, FontStyle.Normal);
        Label("VitoriaEnter", victory.transform, "Aperte Enter para jogar novamente", 40, TextAnchor.MiddleCenter, center, center, center,
              new Vector2(0f, -150f), new Vector2(1400f, 60f), new Color(1f, 1f, 1f, 0.85f), FontStyle.Normal);
        hud.victoryPanel = victory.gameObject;

        gameOver.gameObject.SetActive(false);
        victory.gameObject.SetActive(false);
        return hud;
    }
}
