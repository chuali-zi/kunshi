using System;
using System.Linq;
using ConGame.Runtime.Lighting;
using ConGame.Presentation.Lighting;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ConGame.Editor
{
    public static partial class MvMainShotBuilder
    {
        [MenuItem("ConGame/Art/Build Reference Main Shot (Round 2)")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Leave Play Mode before editing room art.");
            var scene = EditorSceneManager.OpenScene(Room);
            MvMainShotAssets.Prepare();
            var old = Find("MV Main Shot Details");
            if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
            var details = new GameObject("MV Main Shot Details").transform;
            details.SetParent(Find("Environment"), false);

            var wallpaper = MvMainShotAssets.Material("CreamFloral", Color.white, .05f, MvMainShotAssets.Wallpaper());
            var carpet = MvMainShotAssets.Material("ForestGreenCarpet", Color.white, .02f, MvMainShotAssets.Carpet());
            var leather = MvMainShotAssets.Material("OxbloodLeather", new Color(.28f,.045f,.065f), .32f);
            var linen = MvMainShotAssets.Material("IvoryLinen", new Color(.88f,.86f,.79f), .04f);
            var wood = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Art/Materials/MAT_Wood_Walnut.mat");
            if (wood == null) wood = MvMainShotAssets.Material("DarkWalnut", new Color(.115f,.065f,.032f), .22f);
            var ink = MvMainShotAssets.Material("DarkFrame", new Color(.026f,.021f,.017f), .25f);
            var brass = MvMainShotAssets.Material("AgedBrass", new Color(.26f,.18f,.08f), .4f);
            var cream = MvMainShotAssets.Material("CreamPaint", new Color(.69f,.65f,.53f), .17f);

            foreach (var r in All<Renderer>())
            {
                var materials = r.sharedMaterials;
                for (int i=0; i<materials.Length; i++)
                {
                    if (materials[i] == null) continue;
                    string name = materials[i].name;
                    if ((name.Contains("Wallpaper") || name == "CreamFloral") && !name.Contains("Damage")) materials[i]=wallpaper;
                    else if (name.Contains("Floor_DarkGreen") || name == "ForestGreenCarpet") materials[i]=carpet;
                    else if (name.Contains("Leather_Burgundy") || name == "OxbloodLeather") materials[i]=leather;
                    else if (name.Contains("Fabric_Bedding") || name == "IvoryLinen") materials[i]=linen;
                }
                r.sharedMaterials=materials;
            }

            // Fill the architectural window cutout; the gameplay window stays reachable on the return wall.
            var window = Find("PRP_BoardedWindow");
            if (window != null)
            {
                window.position = new Vector3(-2.995f,1.42f,-.65f);
                window.rotation = Quaternion.Euler(0,90,0);
            }
            // A continuous planar back wall also gives the floral print an even physical scale.
            foreach (var r in All<Renderer>().Where(r => r.name == "Shell_Wall_N")) r.enabled=false;
            Wall(details,"Floral Back Wall",new Vector3(0,1.55f,2.595f),new Vector2(6.2f,3.1f),wallpaper);
            foreach(var r in All<Renderer>().Where(r=>r.name=="Shell_Wall_W")) r.enabled=false;
            Wall(details,"Floral Left Return",new Vector3(-3.095f,1.55f,0),new Vector2(5.2f,3.1f),wallpaper).localRotation=Quaternion.Euler(0,-90,0);
            // Tiny rose sprigs: 8 repeats per 1.8 metres, measured against the real wide frame.
            var painting = Child(details,"Framed Dark Painting");
            painting.position=new Vector3(-1.62f,1.94f,2.535f);
            Box(painting,"Canvas backing",Vector3.zero,new Vector3(1.39f,.94f,.055f),ink);
            var art = MvMainShotAssets.Material("PaintingCanvas",Color.white,.07f,MvMainShotAssets.Painting());
            Wall(painting,"Impossible architecture",new Vector3(0,0,-.035f),new Vector2(1.25f,.8f),art,false);
            foreach(int sign in new[]{-1,1})
            {
                Box(painting,"Carved frame",new Vector3(sign*.685f,0,-.04f),new Vector3(.065f,.95f,.065f),ink);
                Box(painting,"Carved frame",new Vector3(0,sign*.455f,-.04f),new Vector3(1.39f,.065f,.065f),ink);
                Box(painting,"Inner gilt",new Vector3(sign*.639f,0,-.078f),new Vector3(.012f,.83f,.013f),brass);
                Box(painting,"Inner gilt",new Vector3(0,sign*.409f,-.078f),new Vector3(1.28f,.012f,.013f),brass);
            }

            ConfigureFurniture(details,wood,leather,linen,cream,ink);
            foreach(string name in new[]{"North Wall","South Wall","West Wall"})
            {
                var boundary=Find(name);boundary.position=new Vector3(boundary.position.x,1.55f,boundary.position.z);
                boundary.localScale=new Vector3(boundary.localScale.x,3.1f,boundary.localScale.z);
            }
            Move("Shell_Ceiling",new Vector3(0,3.15f,0));
            Move("Ceiling",new Vector3(0,3.15f,0));
            var ceilingPaint=MvMainShotAssets.Material("ShadowedCeiling",new Color(.42f,.38f,.31f),.02f);
            foreach(var r in All<Renderer>().Where(r=>r.name=="Shell_Ceiling")) r.enabled=false;
            Box(details,"Plain shadowed ceiling",new Vector3(0,3.13f,0),new Vector3(6.2f,.06f,5.2f),ceilingPaint);
            // Ground the controller root; the chair pose supplies its own visual drop.
            var player=Find("Player");
            player.position=new Vector3(player.position.x,0,player.position.z);
            // CharacterController settles one skin width above the floor; keep the visible soles below that root.
            Find("CH_Player_Model").localPosition=Vector3.down*player.GetComponent<CharacterController>().skinWidth;
            ConfigureDoor(details,wallpaper,cream);
            ConfigureMainCamera(details);
            ConfigureLighting();
            ConfigurePostProcessRenderer();
            ConfigureMainGrade();
            AddOutlet(details,cream,ink);
            // Carpet covers the trap rug too, so the floor reads as one green field.
            var rug = Find("PRP_Rug");
            if (rug != null) foreach(var r in rug.GetComponentsInChildren<Renderer>(true))
                r.sharedMaterials=r.sharedMaterials.Select(_=>carpet).ToArray();
            ConGameArtInstaller.RebuildNavMesh();
            ValidateTacticalNavigation();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("MV_MAIN_SHOT_BUILD: reference layout, generated details, fixed camera and linked lighting saved.");
            Capture();
        }

        static void ValidateTacticalNavigation()
        {
            var anchors=Find("KillerTacticalAnchors");
            if(!UnityEngine.AI.NavMesh.SamplePosition(Find("TA_Entry").position,out var entry,1,UnityEngine.AI.NavMesh.AllAreas))
                throw new InvalidOperationException("Doorway entry is outside baked navigation.");
            foreach(Transform anchor in anchors)
            {
                var path=new UnityEngine.AI.NavMeshPath();
                if(!UnityEngine.AI.NavMesh.SamplePosition(anchor.position,out var target,1,UnityEngine.AI.NavMesh.AllAreas)
                    ||target.position.y>=.25f
                    ||!UnityEngine.AI.NavMesh.CalculatePath(entry.position,target.position,UnityEngine.AI.NavMesh.AllAreas,path)
                    ||path.status!=UnityEngine.AI.NavMeshPathStatus.PathComplete)
                    throw new InvalidOperationException(anchor.name+" must sample reachable ground navigation.");
            }
            Debug.Log("MV_MAIN_SHOT_NAVIGATION: all tactical anchors reachable on the floor.");
        }

        static void ConfigureFurniture(Transform details,Material wood,Material leather,Material linen,Material cream,Material ink)
        {
            var chair = Find("FUR_Armchair");
            chair.position = new Vector3(-1.45f,.575f,1.3f);
            chair.rotation = Quaternion.Euler(0,168,0);
            foreach(var r in chair.GetComponentsInChildren<Renderer>(true)) r.enabled=false;
            var previous=chair.Find("MV Wingback");
            if(previous!=null) UnityEngine.Object.DestroyImmediate(previous.gameObject);
            var wingback=Child(chair,"MV Wingback");
            wingback.localPosition=new Vector3(0,-.575f,0);
            wingback.localScale=Vector3.one*.85f;
            // The host faces local +Z; all details remain attached to the existing hideable chair.
            BuildWingback(wingback,leather,wood);
            // Raise the wooden seat to meet the pelvis while its legs stay on the carpet.
            foreach(Transform visual in Find("FUR_WoodChair"))
                if(visual.name.Contains("Visual"))
                    visual.localScale=new Vector3(visual.localScale.x,1.12f,visual.localScale.z);
            var rear=Find("Zone_SofaRear");
            if(rear!=null) rear.position=new Vector3(-1.56f,.8f,1.92f);

            // Reuse the delivered bookcase and books, extending its silhouette up the left edge.
            var bookcase=Find("FUR_Bookcase");
            bookcase.position=new Vector3(-2.91f,1.05f,.9f);
            foreach(Transform child in bookcase)
                if(child.name.Contains("Visual")) child.localScale=new Vector3(1.6f,1.25f,1);
            var bookCollider=bookcase.GetComponent<BoxCollider>();
            if(bookCollider!=null) { bookCollider.size=new Vector3(1.6f,2.62f,.35f);bookCollider.center=new Vector3(0,.26f,0); }
            var books=Find("PRP_Books");
            if(books!=null) books.position=new Vector3(-2.70f,.54f,.9f);

            // Lamp on a slender table, between the case and the wingback.
            var dresser=Find("FUR_Dresser");
            // The dresser remains on the unseen front wall and keeps its drawers/carry interaction.
            if(dresser!=null) dresser.position=new Vector3(-2.65f,.425f,-1.85f);
            var table=Child(details,"Lamp Side Table");
            table.position=new Vector3(-2.34f,0,1.82f);
            Box(table,"Table top",new Vector3(0,.78f,0),new Vector3(.66f,.05f,.44f),wood).gameObject.AddComponent<BoxCollider>();
            foreach(int x in new[]{-1,1}) foreach(int z in new[]{-1,1})
                Box(table,"Tapered leg",new Vector3(x*.265f,.38f,z*.16f),new Vector3(.038f,.76f,.038f),wood);
            Box(table,"Apron",new Vector3(0,.69f,-.18f),new Vector3(.58f,.13f,.035f),wood);
            var lamp=Find("PRP_TableLamp");
            if(lamp!=null) lamp.position=new Vector3(-2.34f,1.03f,1.82f);
            var remote=Find("PRP_Remote");
            if(remote!=null) remote.position=new Vector3(-2.1f,.825f,1.72f);

            // Retain the bed's gameplay object, hide anchors and collider; replace only its dressed silhouette.
            var bed=Find("FUR_Bed");
            bed.position=new Vector3(1.05f,.30f,1.8f);
            foreach(var r in bed.GetComponentsInChildren<Renderer>(true)) r.enabled=false;
            var bedOld=bed.Find("MV White Bed");
            if(bedOld!=null) UnityEngine.Object.DestroyImmediate(bedOld.gameObject);
            var bedVisual=Child(bed,"MV White Bed");
            bedVisual.localPosition=new Vector3(0,-.30f,0);
            bedVisual.localRotation=Quaternion.Inverse(bed.rotation);
            bedVisual.localScale=new Vector3(.92f,1,.88f);
            BuildBed(bedVisual,wood,linen);
            var collider=bed.GetComponent<BoxCollider>();
            if(collider!=null) collider.size=new Vector3(1.4f,.60f,1.55f);
            Move("Zone_UnderBed",new Vector3(1.05f,.45f,1.8f));
            Move("Zone_Bed",new Vector3(1.05f,.95f,1.8f));
            Move("PRP_BedTopHide",new Vector3(1.05f,.75f,1.8f));
            Move("PRP_Blanket",new Vector3(1.05f,.65f,1.78f));
            // The interactive blanket provides the folds and still switches to trap/dragged states.
            var blanket=Find("PRP_Blanket");
            if(blanket!=null)
                foreach(Transform t in blanket) if(t.name.Contains("Visual")) t.localScale=new Vector3(.72f,1,.95f);
            var crumpled=Find("PRP_Blanket_Crumpled_Visual");
            if(crumpled!=null)
            {
                crumpled.localPosition=Vector3.zero;crumpled.localScale=Vector3.one;crumpled.localRotation=Quaternion.identity;
                var cloth=MvMainShotAssets.Surface("RumpledWhiteLinen",70,72,(u,v)=>
                {
                    float wave=Mathf.Sin(u*14+Mathf.Sin(v*5)*.8f)*Mathf.Sin(v*8+u*3)*.013f;
                    float mound=.065f*Mathf.Exp(-((u-.66f)*(u-.66f)*22+(v-.38f)*(v-.38f)*25));
                    return new Vector3((u-.5f)*1.25f,.045f+wave+mound,(v-.5f)*1.35f);
                });
                crumpled.GetComponent<MeshFilter>().sharedMesh=cloth;
            }
            // The delivered nightstand and white vase are kept.
            var nightstand=Find("FUR_SideTable_Visual");
            if(nightstand!=null) nightstand.position=new Vector3(2.22f,.20f,2.10f);
            Move("PRP_Vase",new Vector3(2.22f,.925f,2.1f));
            Move("PRP_WallClock",new Vector3(1.72f,2.35f,2.51f));
            var clock=Find("PRP_WallClock");
            if(clock!=null) clock.localScale=Vector3.one*1.22f;
            Move("PRP_CeilingLamp",new Vector3(.63f,2.95f,1.25f));

            foreach(var r in All<Renderer>())
                if(r.name=="Door_Panel") r.sharedMaterials=r.sharedMaterials.Select(_=>cream).ToArray();
        }

        static void BuildWingback(Transform parent,Material leather,Material wood)
        {
            Rounded(parent,"Seat base",new Vector3(0,.32f,-.01f),new Vector3(.88f,.27f,.78f),leather);
            Rounded(parent,"Loose seat cushion",new Vector3(0,.49f,.065f),new Vector3(.73f,.16f,.66f),leather);
            Rounded(parent,"High back",new Vector3(0,.91f,-.32f),new Vector3(.87f,.97f,.21f),leather);
            var mesh=MvMainShotAssets.Surface("TuftedWingback",72,80,(u,v)=>
            {
                float x=(u-.5f)*.76f,y=.57f+v*.75f;
                float a=u*4+v*4,b=u*4-v*4;
                float pillow=Mathf.Pow(Mathf.Abs(Mathf.Sin(a*Mathf.PI)*Mathf.Sin(b*Mathf.PI)),.55f);
                return new Vector3(x,y,-.186f+.057f*pillow-.055f*v);
            });
            // Surface helper faces -Z by default; the padded back faces +Z in chair space.
            ReverseFaces(mesh);
            MeshObject(parent,"Diamond tufted leather",mesh,leather);
            for(int row=0;row<6;row++) for(int col=0;col<4;col++)
            {
                float x=-.285f+col*.19f+(row%2)*.095f;
                if(x>.37f) continue;
                Rounded(parent,"Covered button",new Vector3(x,.625f+row*.125f,-.181f-row*.009f),new Vector3(.022f,.022f,.012f),leather);
            }
            foreach(int side in new[]{-1,1})
            {
                var wing=Rounded(parent,"Upholstered wing",new Vector3(side*.435f,1.035f,-.19f),new Vector3(.16f,.63f,.35f),leather);
                wing.localRotation=Quaternion.Euler(-8,side*14,-side*5);
                Rounded(parent,"Arm",new Vector3(side*.46f,.57f,.025f),new Vector3(.18f,.43f,.8f),leather);
                Rounded(parent,"Rolled arm",new Vector3(side*.46f,.79f,.05f),new Vector3(.22f,.14f,.82f),leather);
                foreach(int z in new[]{-1,1})
                    Box(parent,"Wood foot",new Vector3(side*.35f,.085f,z*.29f),new Vector3(.065f,.17f,.065f),wood);
            }
        }

        static void BuildBed(Transform p,Material wood,Material linen)
        {
            foreach(int x in new[]{-1,1}) foreach(int z in new[]{-1,1})
                Box(p,"Bed leg",new Vector3(x*.66f,.23f,z*.72f),new Vector3(.075f,.46f,.075f),wood);
            Box(p,"Front rail",new Vector3(0,.41f,-.80f),new Vector3(1.47f,.18f,.08f),wood);
            Box(p,"Back rail",new Vector3(0,.43f,.8f),new Vector3(1.47f,.2f,.08f),wood);
            foreach(int x in new[]{-1,1}) Box(p,"Side rail",new Vector3(x*.71f,.43f,0),new Vector3(.06f,.18f,1.6f),wood);
            Rounded(p,"White mattress",new Vector3(0,.54f,0),new Vector3(1.42f,.22f,1.62f),linen);
            Rounded(p,"Pillow",new Vector3(0,.73f,.55f),new Vector3(.93f,.16f,.43f),linen);
            // Soft overhang across the front edge; the top stays below the interactive blanket.
            var sheet=MvMainShotAssets.Surface("LinenFrontDrape",70,24,(u,v)=>
            {
                float fold=Mathf.Sin(u*19)*.012f+Mathf.Sin(u*31+.6f)*.005f;
                return new Vector3((u-.5f)*1.46f,.66f-v*.28f+.025f*Mathf.Sin(u*15+v*2),-.81f-(.085f+fold)*Mathf.Sin(v*Mathf.PI*.5f));
            });
            ReverseFaces(sheet);
            MeshObject(p,"Hanging white sheet",sheet,linen);
        }

        static void ConfigureDoor(Transform details,Material wallpaper,Material cream)
        {
            // Bring the doorway into the right return of the diorama. Translate the complete entry group,
            // including the blocker, trap and killer spawn, so visual and physical entry still coincide.
            var hinge=Find("DoorHinge");
            float delta=2.35f-hinge.position.z;
            var doorSet=Find("ENV_DoorSet_Visual");
            var panel=Find("Door_Panel");
            var handle=Find("Door_Handle");
            if(handle.parent!=panel) handle.SetParent(panel,true);
            if(panel.parent!=hinge) panel.SetParent(hinge,true);
            hinge.position+=Vector3.forward*delta;
            doorSet.position+=Vector3.forward*delta;
            foreach(string name in new[]{"ENV_BlackCorridor_Visual","Corridor Floor","Black Corridor","PRP_RoomDoor_Interact","PRP_DoorTrapSocket","PRP_AmbushPoint","Zone_DoorBlind","Killer","TA_Entry","TA_DoorBlind","KillerEntryInside"})
            {
                var t=Find(name);if(t!=null) t.position+=Vector3.forward*delta;
            }
            Move("KillerEntryInside",new Vector3(2.2f,0,1.9f));
            var corridorZone=Find("Zone_BlackCorridor");
            if(corridorZone==null)
            {
                corridorZone=Child(Find("Room_M1"),"Zone_BlackCorridor");
                var trigger=corridorZone.gameObject.AddComponent<BoxCollider>();
                trigger.isTrigger=true;trigger.size=new Vector3(1.6f,2.2f,1.4f);
                corridorZone.gameObject.AddComponent<ConGame.Runtime.Interaction.CorridorResistanceVolume>().Configure(Vector3.right,0,1.6f);
            }
            corridorZone.position=new Vector3(3.6f,.9f,1.9f);
            panel.localScale=new Vector3(1,1.15f,1);
            var frame=Find("Door_Frame");frame.localScale=new Vector3(1,1.15f,1);
            var leaf=hinge.Find("Door");leaf.localPosition=new Vector3(0,1.18f,-.45f);leaf.localScale=new Vector3(.12f,2.36f,.9f);
            foreach(var r in All<Renderer>().Where(r=>r.name=="Shell_Wall_E")) r.enabled=false;
            var lower=Find("East Wall Lower");lower.position=new Vector3(3.15f,1.55f,-.575f);lower.localScale=new Vector3(.1f,3.1f,4.05f);
            var upper=Find("East Wall Upper");upper.position=new Vector3(3.15f,1.55f,2.475f);upper.localScale=new Vector3(.1f,3.1f,.25f);
            Move("Door Lintel",new Vector3(3.15f,2.73f,1.9f));
            Find("Door Lintel").localScale=new Vector3(.1f,.74f,.9f);
            Wall(details,"Right floral return",new Vector3(3.095f,1.55f,-.575f),new Vector2(4.05f,3.1f),wallpaper).localRotation=Quaternion.Euler(0,90,0);
            Wall(details,"Right door jamb return",new Vector3(3.095f,1.55f,2.475f),new Vector2(.25f,3.1f),wallpaper).localRotation=Quaternion.Euler(0,90,0);
            Wall(details,"Right door lintel",new Vector3(3.095f,2.73f,1.9f),new Vector2(.9f,.74f),wallpaper).localRotation=Quaternion.Euler(0,90,0);
            // Keep a clean cream leaf with its original handle and hinged breach animation.
            panel.GetComponent<Renderer>().sharedMaterial=cream;
            Move("TA_TableLamp",new Vector3(-1.75f,0,.22f));
            Move("TA_SofaCheck",new Vector3(-.63f,0,1.50f));
            Move("TA_DoorBlind",new Vector3(2.6f,0,1f));
        }

        static void ReverseFaces(Mesh mesh)
        {
            var indices=mesh.triangles;
            for(int i=0;i<indices.Length;i+=3) {int a=indices[i];indices[i]=indices[i+2];indices[i+2]=a;}
            mesh.triangles=indices;mesh.RecalculateNormals();EditorUtility.SetDirty(mesh);
        }

        static void ConfigureMainCamera(Transform details)
        {
            // A distant, longer-lens view flattens the room into a stage diorama.
            Vector3 pos=new Vector3(0,1.55f,-5.2f);
            Quaternion rot=Quaternion.Euler(3,0,0);
            var camera=Camera.main;
            camera.transform.SetPositionAndRotation(pos,rot);
            camera.fieldOfView=36;
            camera.nearClipPlane=.03f;
            camera.farClipPlane=35;
            camera.allowHDR=true;
            // Look through the stage's front wall without changing room collisions or the first-person view.
            const int cutawayLayer=30;
            var tags=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            tags.FindProperty("layers").GetArrayElementAtIndex(cutawayLayer).stringValue="MainShotCutaway";
            tags.ApplyModifiedPropertiesWithoutUndo();
            foreach(var r in All<Renderer>().Where(r=>r.name=="Shell_Wall_S"||r.name=="Shell_Baseboard_S"))
                r.gameObject.layer=cutawayLayer;
            foreach(string name in new[]{"PRP_CRTTV","FUR_TVCabinet_Visual","PRP_DrawerClutter_Visual"})
                foreach(var r in Find(name).GetComponentsInChildren<Renderer>(true)) r.gameObject.layer=cutawayLayer;
            // Movable furniture must remain visible when carried into the stage.
            foreach(var r in Find("FUR_Dresser").GetComponentsInChildren<Renderer>(true)) r.gameObject.layer=0;
            camera.cullingMask &= ~(1<<cutawayLayer);
            var cameraMode=new SerializedObject(All<ConGame.Presentation.Camera.CameraModeController>().Single());
            cameraMode.FindProperty("fixedViewHiddenLayers").intValue=1<<cutawayLayer;
            var viewAnchor=Child(details,"Main Shot Camera Anchor");
            viewAnchor.SetPositionAndRotation(pos,rot);
            cameraMode.FindProperty("fixedViewAnchor").objectReferenceValue=viewAnchor;
            cameraMode.ApplyModifiedPropertiesWithoutUndo();
            var data=camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing=true;
            data.volumeLayerMask=1;
            data.antialiasing=AntialiasingMode.FastApproximateAntialiasing;
            data.dithering=true;
            var rig=Find("CM Fixed Television Camera");
            rig.SetPositionAndRotation(pos,rot);
            var screen=Find("CameraScreenAnchor");
            if(screen!=null)
            {
                // This is the physical TV glass / ending observer entry, independent of the distant lens.
                screen.rotation=Quaternion.Euler(2.088f,0,0);
                screen.position=new Vector3(0,1.38f,-2.11f)-screen.forward*.04f;
            }
            foreach(var component in rig.GetComponents<Component>())
            {
                var so=new SerializedObject(component);
                var fov=so.FindProperty("m_Lens.FieldOfView");
                if(fov==null) continue;
                fov.floatValue=camera.fieldOfView;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        static void ConfigureLighting()
        {
            var ceiling=Find("PRP_CeilingLamp");
            var table=Find("PRP_TableLamp");
            ConfigurePractical(ceiling,1.1f,7);
            ConfigurePractical(table,.35f,4.5f);
            foreach(var bounce in All<PracticalLightBounce>())
            {
                float ratio=bounce.name=="RoomBounce"?1.1f:bounce.name=="CeilingBounce"?.28f:.35f;
                bounce.Configure(bounce.SourceLight,bounce.TargetLight,ratio);
            }
            RenderSettings.ambientMode=AmbientMode.Flat;
            RenderSettings.ambientLight=new Color(.043f,.038f,.028f);
            RenderSettings.reflectionIntensity=.18f;
        }

        static void ConfigurePractical(Transform host,float intensity,float range)
        {
            var emitter=host.Find("PracticalEmitter");
            if(emitter==null) throw new InvalidOperationException(host.name+" missing wired practical light.");
            var light=emitter.GetComponent<Light>();
            light.color=new Color(1,.85f,.66f);
            light.intensity=intensity;
            light.range=range;
            light.shadows=LightShadows.Soft;
            light.shadowStrength=.72f;
            host.GetComponent<BreakableLight>().Configure(light);
        }

        static void ConfigurePostProcessRenderer()
        {
            var pipeline=GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if(pipeline==null) throw new InvalidOperationException("The main shot requires URP.");
            pipeline.supportsHDR=true;EditorUtility.SetDirty(pipeline);
            pipeline.colorGradingMode=ColorGradingMode.HighDynamicRange;
            var renderer=AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/_Game/Content/Config/RP_ConGame_Renderer.asset");
            renderer.postProcessData=AssetDatabase.LoadAssetAtPath<PostProcessData>("Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset");
            if(renderer.postProcessData==null) throw new InvalidOperationException("URP post-process resources are missing.");
            renderer.SetDirty();EditorUtility.SetDirty(renderer);
        }

        static void ConfigureMainGrade()
        {
            var volume=All<Volume>().First(v=>v.name=="MV Room Grade");
            var profile=volume.sharedProfile;
            var color=Get<ColorAdjustments>(profile);
            color.postExposure.Override(.6f);
            color.contrast.Override(9);
            color.saturation.Override(-10);
            Get<Tonemapping>(profile).mode.Override(TonemappingMode.ACES);
            Get<WhiteBalance>(profile).temperature.Override(11);
            var vignette=Get<Vignette>(profile);
            vignette.intensity.Override(.32f);vignette.smoothness.Override(.65f);
            var grain=Get<FilmGrain>(profile);
            grain.type.Override(FilmGrainLookup.Medium1);grain.intensity.Override(.28f);grain.response.Override(.65f);
            var bloom=Get<Bloom>(profile);
            bloom.intensity.Override(.12f);bloom.threshold.Override(1.25f);
            foreach(var component in profile.components) EditorUtility.SetDirty(component);
            EditorUtility.SetDirty(profile);
        }

        static void AddOutlet(Transform p,Material cream,Material ink)
        {
            var outlet=Child(p,"Wall power outlet");
            outlet.position=new Vector3(.48f,.37f,2.57f);
            Box(outlet,"Ceramic plate",Vector3.zero,new Vector3(.12f,.17f,.018f),cream);
            foreach(int y in new[]{-1,1}) foreach(int x in new[]{-1,1})
                Box(outlet,"Socket",new Vector3(x*.021f,y*.035f,-.012f),new Vector3(.006f,.018f,.006f),ink);
        }

        static Transform Wall(Transform parent,string name,Vector3 position,Vector2 size,Material material,bool physicalUV=true)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=position;
            go.transform.localScale=new Vector3(size.x,size.y,1);
            go.GetComponent<Renderer>().sharedMaterial=material;
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            if(physicalUV)
            {
                var mesh=UnityEngine.Object.Instantiate(go.GetComponent<MeshFilter>().sharedMesh);
                mesh.name="FloralWallUV";
                mesh.uv=mesh.uv.Select(uv=>new Vector2(uv.x*size.x/1.8f,uv.y*size.y/1.8f)).ToArray();
                string path=MvMainShotAssets.Root+"/"+name.Replace(" ","")+"UV.asset";
                var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if(existing==null) AssetDatabase.CreateAsset(mesh,path);
                else {EditorUtility.CopySerialized(mesh,existing);UnityEngine.Object.DestroyImmediate(mesh);mesh=existing;}
                go.GetComponent<MeshFilter>().sharedMesh=mesh;
            }
            return go.transform;
        }

        static Transform Rounded(Transform p,string n,Vector3 pos,Vector3 size,Material m)
        {
            var t=Box(p,n,pos,size,m);
            t.GetComponent<MeshFilter>().sharedMesh=MvMainShotAssets.UpholsteryMesh();
            return t;
        }

        static Transform Box(Transform p,string n,Vector3 pos,Vector3 scale,Material mat)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name=n;go.transform.SetParent(p,false);go.transform.localPosition=pos;go.transform.localScale=scale;
            go.GetComponent<Renderer>().sharedMaterial=mat;
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            return go.transform;
        }
        static void MeshObject(Transform p,string n,Mesh mesh,Material mat)
        {
            var go=new GameObject(n,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(p,false);
            go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<Renderer>().sharedMaterial=mat;
        }
        static T Get<T>(VolumeProfile profile) where T:VolumeComponent
        {
            if(profile.TryGet<T>(out var value)) return value;
            value=profile.Add<T>(true);AssetDatabase.AddObjectToAsset(value,profile);return value;
        }
        static T[] All<T>() where T:Component => UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include)
            .Where(t=>t.gameObject.scene.IsValid()).ToArray();
        static Transform Find(string n)=>All<Transform>().FirstOrDefault(t=>t.name==n);
        static void Move(string name,Vector3 position) {var t=Find(name);if(t!=null)t.position=position;}
        static Transform Child(Transform p,string name) {var t=new GameObject(name).transform;t.SetParent(p,false);return t;}
    }
}
