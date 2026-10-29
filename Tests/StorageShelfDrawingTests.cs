using System;
using System.Reflection;
using System.Reflection.Emit;
using System.Linq;
using System.Collections.Generic;
using HarmonyLib;
using MagicStorage;
using UnityEngine;
using Verse;

internal static class StorageShelfDrawingTests
{
    private static int checks;
    private static void Check(bool value, string label) { checks++;if(!value) throw new Exception(label); }
    private static void Near(float a, float b, string label) => Check(Math.Abs(a-b)<0.00001f,label+": "+a+" / "+b);
    public static int Main()
    {
        try { Layout(); DynamicLayout(); Routing(); DynamicRouting(); IconRouting(); HiddenShelfRouting(); Console.WriteLine("PASS: "+checks+" shelf layout/drawing assertions (renderer boundaries stubbed).");return 0; }
        catch(Exception ex) { Console.Error.WriteLine(ex);return 1; }
    }

    private static void DynamicLayout()
    {
        foreach (int count in new[] { 1, 2, 4, 5, 6, 9, 12, 16, 25 })
        {
            float size = StorageShelfLayout.ItemSizeFor(count);
            Check(size > 0 && size <= 0.4f, "dynamic item size is positive and bounded");
            for (int i = 0; i < count; i++)
            {
                Vector3 offset = StorageShelfLayout.Offset(i, count);
                Check(Math.Abs(offset.x) + size / 2 <= 0.39001f, "all configured items fit shelf width");
                Check(Math.Abs(offset.z - 0.08f) + size / 2 <= 0.39001f, "all configured items fit shelf depth");
                for (int j = 0; j < i; j++)
                {
                    Vector3 other = StorageShelfLayout.Offset(j, count);
                    Check(Math.Abs(offset.x - other.x) >= size || Math.Abs(offset.z - other.z) >= size,
                        "dynamic positions do not overlap");
                }
                Vector2 fitted = StorageShelfLayout.FitSize(new Vector2(2, 1), 35, count);
                double r = 35 * Math.PI / 180;
                Check(fitted.x * Math.Cos(r) + fitted.y * Math.Sin(r) <= size + 0.00001,
                    "rotated graphics fit dynamic position");
            }
        }
    }

    private class NoGraphicItem : Thing
    {
        public override Graphic Graphic { get => throw new Exception("A custom renderer has no ordinary graphic"); set { } }
    }
    private sealed class CustomPhaseItem : Thing
    { public override void DynamicDrawPhaseAt(DrawPhase phase, Vector3 drawLoc, bool flip = false) { } }
    private sealed class CustomDrawItem : Thing
    { protected override void DrawAt(Vector3 drawLoc, bool flip = false) { } }
    private sealed class CustomPrintItem : Thing
    { public override void Print(SectionLayer layer) { } }

    private sealed class HiddenItem : NoGraphicItem
    { public override void DrawGUIOverlay() { throw new Exception("Hidden custom overlay must never run"); } }

    private static void HiddenShelfRouting()
    {
        var map = new Map();
        var shelf = new Building_StorageSupplyShelf { Map = map, Position = new IntVec3 { x = 4, z = 7 },
            Selections = new ThingDef[100], Stocks = new Thing[100] };
        shelf.def.category = ThingCategory.Building;
        shelf.def.modExtensions.Add(new StorageShelfProperties { hideStoredItems = true });
        map.haulDestinationManager.Parent = shelf;
        var item = new HiddenItem { Map = map, Position = shelf.Position };
        shelf.GetSlotGroup().HeldThings.Add(item);
        foreach (bool tidy in new[] { true, false })
        {
            MagicStorageMod.Settings.TidySupplyShelf = tidy;
            Check(StorageShelfVisibility.IsHidden(item), "hidden shelf ignores tidy toggle");
            Check(!StorageShelfVisibility.IsHidden(shelf), "shelf graphic itself stays visible");
            Check(StorageShelfDrawing.TryGetShelf(item, out _, out int slot) && slot == -1,
                "hidden items bypass stock lookup and layout assignment");
            foreach (DrawerType drawer in new[] { DrawerType.MapMeshOnly, DrawerType.RealtimeOnly, DrawerType.MapMeshAndRealTime })
            {
                item.def.drawerType = drawer;
                Printer_Plane.Calls.Clear(); Graphics.Calls.Clear(); int iconCalls = Widgets.IconCalls;
                Check(!StorageShelfDrawing.PrintItem(new SectionLayer_ThingsGeneral(), item), "hidden static mesh never prints");
                foreach (DrawPhase phase in new[] { DrawPhase.EnsureInitialized, DrawPhase.ParallelPreDraw, DrawPhase.Draw })
                    Check(!StorageShelfDrawing.DrawItem(item, phase), "hidden item skips every dynamic phase");
                Check(Printer_Plane.Calls.Count == 0 && Graphics.Calls.Count == 0 && Widgets.IconCalls == iconCalls,
                    "hidden item neither renders nor requests graphics and thumbnails");
                Check(map.dynamicDrawManager.Drawables.Count == 0, "hidden static items never register icon drawables");
            }
            StorageShelfVisibility.DrawGUIOverlay(item);
            Check(item.OverlayCalls == 0, "hidden custom overlay is skipped before virtual dispatch");
            var queue = typeof(StorageShelfDrawing).GetMethod("QueueOverlay", BindingFlags.Static | BindingFlags.NonPublic);
            Check(!(bool)queue.Invoke(null, new object[] { item }), "hidden state icons are not queued");
            map.haulDestinationManager.Parent = new object();
            Check(!StorageShelfVisibility.IsHidden(item) && (bool)queue.Invoke(null, new object[] { item }),
                "taking an item off the shelf immediately restores native rendering");
            map.haulDestinationManager.Parent = shelf;
        }
        var call = new CodeInstruction(OpCodes.Callvirt, typeof(Thing).GetMethod("DrawGUIOverlay"));
        var filter = typeof(StorageShelfDrawing).GetMethod("FilterGUIOverlays", BindingFlags.Static | BindingFlags.NonPublic);
        var instructions = ((IEnumerable<CodeInstruction>)filter.Invoke(null,
            new object[] { new[] { call, new CodeInstruction(OpCodes.Ret) } })).ToList();
        Check(instructions[0] == call && call.opcode == OpCodes.Call &&
            ((MethodInfo)call.operand).DeclaringType == typeof(StorageShelfVisibility),
            "native overlay loop routes virtual item draws through visibility gate");
        ((MethodInfo)call.operand).Invoke(null, new object[] { item });
        var floorItem = new Thing { Map = map, Position = shelf.Position, Spawned = false };
        StorageShelfVisibility.DrawGUIOverlay(floorItem);
        Check(floorItem.OverlayCalls == 1, "ordinary item overlays retain native behavior");
        shelf.def.modExtensions.Clear();
        Check(!StorageShelfVisibility.IsHidden(item), "ordinary supply shelves do not hide their contents");
        MagicStorageMod.Settings.TidySupplyShelf = true;
    }

    private static void IconRouting()
    {
        var map = new Map();
        var shelf = new Building_StorageSupplyShelf { Map=map, Position=new IntVec3 { x=4,z=7 } };
        map.haulDestinationManager.Parent = shelf;
        var item = new NoGraphicItem { Map=map, Position=shelf.Position };
        item.def.graphicData = null; item.def.drawerType = DrawerType.RealtimeOnly;
        shelf.Selections[0] = item.def; shelf.GetSlotGroup().HeldThings.Add(item);
        var icon = new ResolvedIcon { Texture=new RenderTexture { width=128,height=128 },
            Proportions=new Vector2(1,4), Color=new Color(.3f,.6f,.9f) };
        Widgets.Resolve = thing => icon;
        Widgets.IconCalls=0; Printer_Plane.Calls.Clear(); Graphics.Calls.Clear();
        Check(!StorageShelfDrawing.PrintItem(new SectionLayer_ThingsGeneral(),item),"custom renderers never bake portraits into a static mesh");
        Check(Printer_Plane.Calls.Count==0 && Widgets.IconCalls==0,"static printing neither requests an icon nor reads the missing Graphic");
        Check(StorageShelfDrawing.DrawItem(item,DrawPhase.EnsureInitialized),"custom renderer initialization stays native");
        Check(StorageShelfDrawing.DrawItem(item,DrawPhase.ParallelPreDraw),"custom renderer parallel preparation stays native");
        Check(Widgets.IconCalls==0,"icon and material creation never run in the parallel draw phase");
        Check(!StorageShelfDrawing.DrawItem(item,DrawPhase.Draw),"native icon replaces the missing world graphic");
        Check(Graphics.Calls.Count==1,"thumbnail is drawn once");
        var draw=Graphics.Calls[0];
        Check(draw.Material.Request.mainTex==icon.Texture,"portrait RenderTexture is passed to the map material");
        Check(draw.Material.Request.shader==ShaderDatabase.Transparent,"unmaterialed portraits use a map shader");
        Near(.4f,draw.Size.x,"portrait fits the normal shelf position");
        Near(draw.Size.x,draw.Size.y,"portrait keeps its texture aspect instead of the original body's proportions");
        Near(.3f,draw.Material.Request.color.r,"native icon tint is retained");
        var center=StorageShelfDrawing.Center(item,shelf,0);
        Near(center.x,draw.Center.x,"thumbnail is centered in its assigned slot");
        Near(center.z,draw.Center.z,"thumbnail z matches shelf layout");

        icon.Texture=new RenderTexture { width=128,height=128 };
        StorageShelfDrawing.DrawItem(item,DrawPhase.Draw);
        Check(Graphics.Calls[1].Material.Request.mainTex==icon.Texture,"replaced portrait cache textures are picked up on the next draw");
        shelf.Selections=new ThingDef[25];shelf.Stocks=new Thing[25];shelf.Selections[0]=item.def;
        icon.Texture=new Texture2D { width=256,height=128 };icon.Angle=35f;icon.Scale=3f;
        Graphics.Calls.Clear();StorageShelfDrawing.DrawItem(item,DrawPhase.Draw);draw=Graphics.Calls[0];
        double radians=35*Math.PI/180;
        float extent=(float)Math.Max(draw.Size.x*Math.Cos(radians)+draw.Size.y*Math.Sin(radians),
            draw.Size.x*Math.Sin(radians)+draw.Size.y*Math.Cos(radians));
        Near(StorageShelfLayout.ItemSizeFor(25),extent,"large rotated icons remain inside densely configured shelf positions");
        Near(2f,draw.Size.x/draw.Size.y,"animal-style icon textures preserve aspect ratio");
        Near(35f,draw.Angle,"native icon angle is retained");
        icon.Angle=0;icon.Scale=.5f;Graphics.Calls.Clear();StorageShelfDrawing.DrawItem(item,DrawPhase.Draw);
        Near(StorageShelfLayout.ItemSizeFor(25)*.5f,Graphics.Calls[0].Size.x,"smaller native icon scale is honored");

        var worldShader=new Shader { Name="map masked custom" };var uiShader=new Shader { Name="UI masked custom" };
        DefDatabase<ShaderTypeDef>.AllDefsListForReading.Add(new ShaderTypeDef { Shader=worldShader });
        ShaderDatabase.UI.Add(worldShader,uiShader);
        var request=new MaterialRequest(icon.Texture,uiShader,new Color(.8f,.4f,.2f))
        { maskTex=new Texture2D(),secondaryTex=new Texture2D(),colorTwo=new Color(.6f,.2f,.4f) };
        icon.Material=MaterialPool.MatFrom(request);icon.Color=new Color(.5f,.5f,.5f);
        Graphics.Calls.Clear();StorageShelfDrawing.DrawItem(item,DrawPhase.Draw);draw=Graphics.Calls[0];
        Check(draw.Material.Request.shader==worldShader,"UI shader resolves back to its original map shader");
        Check(draw.Material.Request.maskTex==request.maskTex && draw.Material.Request.secondaryTex==request.secondaryTex,
            "native mask and secondary textures survive shader conversion");
        Near(.4f,draw.Material.Request.color.r,"material primary tint combines with icon tint");
        Near(.3f,draw.Material.Request.colorTwo.r,"material secondary tint combines with icon tint");
        Check(icon.Material.Request.shader==uiShader,"shared native UI material is never modified");

        foreach(var custom in new Thing[] { new CustomPhaseItem(),new CustomDrawItem(),new CustomPrintItem() })
        {
            custom.Map=map;custom.Position=shelf.Position;custom.def.drawerType=DrawerType.MapMeshAndRealTime;
            shelf.Selections=new ThingDef[3];shelf.Stocks=new Thing[3];shelf.Selections[0]=custom.def;
            shelf.GetSlotGroup().HeldThings.Clear();shelf.GetSlotGroup().HeldThings.Add(custom);
            Printer_Plane.Calls.Clear();Graphics.Calls.Clear();int before=Widgets.IconCalls;
            StorageShelfDrawing.PrintItem(new SectionLayer_ThingsGeneral(),custom);
            Check(Printer_Plane.Calls.Count==0,"overridden draw entry points automatically select thumbnails");
            Check(!StorageShelfDrawing.DrawItem(custom,DrawPhase.Draw) && Graphics.Calls.Count==1,
                "hybrid custom renderers draw their thumbnail exactly once");
            Check(Widgets.IconCalls==before+1,"arbitrary custom types use the vanilla resolver without a corpse check");
            custom.def.drawerType=DrawerType.MapMeshOnly;Graphics.Calls.Clear();
            StorageShelfDrawing.PrintItem(new SectionLayer_ThingsGeneral(),custom);
            StorageShelfDrawing.PrintItem(new SectionLayer_ThingsGeneral(),custom);
            Check(map.dynamicDrawManager.Drawables.Count==1 && map.dynamicDrawManager.Drawables[0]==custom,
                "pure static custom objects enter the actual dynamic draw list exactly once");
            Check(!StorageShelfDrawing.DrawItem(custom,DrawPhase.Draw) && Graphics.Calls.Count==1,
                "custom mesh-only objects can use live portrait textures too");
            typeof(StorageShelfDrawing).GetMethod("ReleaseIconDrawable",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[] { custom });
            Check(map.dynamicDrawManager.Drawables.Count==0,"despawning removes the additional dynamic registration");
        }
        var ordinary=new ThingWithComps { Map=map,Position=shelf.Position };
        ordinary.def.drawerType=DrawerType.RealtimeOnly;
        shelf.Selections[0]=ordinary.def;shelf.Stocks=new Thing[3];
        shelf.GetSlotGroup().HeldThings.Clear();shelf.GetSlotGroup().HeldThings.Add(ordinary);
        int calls=Widgets.IconCalls;Graphics.Calls.Clear();
        StorageShelfDrawing.DrawItem(ordinary,DrawPhase.Draw);
        Check(Widgets.IconCalls==calls,"standard ThingWithComps rendering keeps the existing graphic path");
        Check(Graphics.Calls[0].Material==ordinary.Graphic.Material,"ordinary items preserve their original map material");
        ordinary.Graphic=BaseContent.BadGraphic;Graphics.Calls.Clear();StorageShelfDrawing.DrawItem(ordinary,DrawPhase.Draw);
        Check(Widgets.IconCalls==calls+1 && Graphics.Calls.Count==1,"invalid ordinary graphics automatically fall back to an icon");
        icon.Texture=BaseContent.BadTex;Graphics.Calls.Clear();
        Check(StorageShelfDrawing.DrawItem(ordinary,DrawPhase.Draw) && Graphics.Calls.Count==0,
            "a missing native icon falls back to the object's original renderer instead of drawing a red cross");
        ordinary.def.drawerType=DrawerType.MapMeshOnly;
        StorageShelfDrawing.PrintItem(new SectionLayer_ThingsGeneral(),ordinary);
        map.listerThings.AllThings.Add(ordinary);Find.Maps.Add(map);
        Check(map.dynamicDrawManager.Drawables.Count==1,"mesh-only missing graphics get dynamic registration too");
        calls=Widgets.IconCalls;MagicStorageMod.Settings.TidySupplyShelf=false;
        StorageShelfDrawing.RefreshAll();
        Check(map.dynamicDrawManager.Drawables.Count==0,"disabling tidy shelves releases additional registrations");
        Check(StorageShelfDrawing.DrawItem(ordinary,DrawPhase.Draw) && Widgets.IconCalls==calls,"disabled shelf layout never resolves thumbnails");
        MagicStorageMod.Settings.TidySupplyShelf=true;map.haulDestinationManager.Parent=new object();
        Check(StorageShelfDrawing.DrawItem(ordinary,DrawPhase.Draw) && Widgets.IconCalls==calls,"objects outside supply shelves retain native rendering");
        DefDatabase<ShaderTypeDef>.AllDefsListForReading.Clear();
        Widgets.Resolve=thing=>new ResolvedIcon();
    }

    private static void DynamicRouting()
    {
        var map = new Map();
        var shelf = new Building_StorageSupplyShelf { Map = map, Position = new IntVec3 { x = 10, z = 20 },
            Selections = new ThingDef[9], Stocks = new Thing[9] };
        map.haulDestinationManager.Parent = shelf;
        var def = new ThingDef();
        for (int i = 0; i < 9; i++)
        {
            shelf.Selections[i] = def;
            var item = new Thing { Map = map, Position = shelf.Position, def = def };
            shelf.GetSlotGroup().HeldThings.Add(item);
            Check(StorageShelfDrawing.TryGetShelf(item, out _, out int slot) && slot == i,
                "real stack uses its configured position beyond third");
            Printer_Plane.Calls.Clear();
            StorageShelfDrawing.PrintItem(new SectionLayer_ThingsGeneral(), item);
            Near(StorageShelfLayout.ItemSizeFor(9), Printer_Plane.Calls[0].Size.x,
                "static graphics use dynamic item size");
        }
    }

    private static void Layout()
    {
        Near(-0.15625f,StorageShelfLayout.Offset(0).x,"upper left matches approved draft");
        Near(0.15625f,StorageShelfLayout.Offset(1).x,"upper right matches approved draft");
        Near(0.25f,StorageShelfLayout.Offset(0).z,"upper row height");
        Near(0.25f,StorageShelfLayout.Offset(1).z,"upper row aligned");
        Near(0f,StorageShelfLayout.Offset(2).x,"lower row centered");
        Near(-0.03125f-0.06f,StorageShelfLayout.Offset(2).z,"lower row moved down 0.06 cells");
        foreach(var source in new[] { new Vector2(1,1),new Vector2(2,1),new Vector2(1,3),new Vector2(0.1f,0.1f) })
            foreach(float angle in new[] { 0f,35f,90f,180f,270f })
            {
                Vector2 size=StorageShelfLayout.FitSize(source,angle);
                double r=angle*Math.PI/180,cos=Math.Abs(Math.Cos(r)),sin=Math.Abs(Math.Sin(r));
                float w=(float)(size.x*cos+size.y*sin),h=(float)(size.x*sin+size.y*cos);
                Check(w<=0.40001f && h<=0.40001f,"rotated sprite fits approved square");
                Near(0.4f,Math.Max(w,h),"sprite fills the available extent");
                Near(source.x/source.y,size.x/size.y,"preserves aspect ratio");
            }
    }

    private static void Routing()
    {
        var map=new Map();
        var shelf=new Building_StorageSupplyShelf { Map=map,Position=new IntVec3 { x=10,z=20 } };
        shelf.def.Altitude=1f;
        map.haulDestinationManager.Parent=shelf;
        var item=new Thing { Map=map,Position=shelf.Position };
        shelf.GetSlotGroup().HeldThings.Add(item);
        shelf.Selections[1]=item.def;
        var layer=new SectionLayer_ThingsGeneral();
        Check(StorageShelfDrawing.TryGetShelf(item,out var found,out int slot),"recognizes shelf item");
        Check(found==shelf && slot==1,"slot follows configured type, not spawn order");
        Check(!StorageShelfDrawing.PrintItem(layer,item),"replaces native scatter rendering");
        var print=Printer_Plane.Calls[0];
        Near(10.5f+0.15625f,print.Center.x,"static x"); Near(20.75f,print.Center.z,"static z");
        Check(print.Center.y>shelf.def.Altitude,"draws above the shelf frame");
        Near(0.4f,print.Size.x,"static square width");
        Near(1f,item.Graphic.drawSize.x,"never changes shared graphic size");

        item.def.drawerType=DrawerType.RealtimeOnly;
        Check(StorageShelfDrawing.DrawItem(item,DrawPhase.EnsureInitialized),"initialization remains native");
        Check(!StorageShelfDrawing.DrawItem(item,DrawPhase.Draw),"replaces realtime drawing");
        Near(print.Center.x,Graphics.Calls[0].Center.x,"static and realtime x agree");
        Near(print.Center.z,Graphics.Calls[0].Center.z,"static and realtime z agree");
        Near(print.Size.x,Graphics.Calls[0].Size.x,"static and realtime scale agree");
        Graphics.Calls.Clear();item.def.drawerType=DrawerType.MapMeshAndRealTime;
        Check(!StorageShelfDrawing.DrawItem(item,DrawPhase.Draw) && Graphics.Calls.Count==0,"does not draw hybrid items twice");

        var stack=new Graphic_StackCount();stack.Stack.Material.Name="full stack";
        item.Graphic=stack;item.stackCount=75;Printer_Plane.Calls.Clear();
        StorageShelfDrawing.PrintItem(layer,item);
        Check(Printer_Plane.Calls[0].Material==stack.Stack.Material,"preserves actual stack-count material");
        var random=new Graphic_Random();item.Graphic=random;Printer_Plane.Calls.Clear();StorageShelfDrawing.PrintItem(layer,item);
        Check(Printer_Plane.Calls[0].Material==random.Variant.Material,"preserves random material variant");

        item.Graphic=new Graphic { drawSize=new Vector2(2,1),WestFlipped=true,data=new GraphicData { flipExtraRotation=35 } };
        item.Rotation=Rot4.West;Printer_Plane.Calls.Clear();StorageShelfDrawing.PrintItem(layer,item);
        Check(Printer_Plane.Calls[0].Flip,"preserves flipped UVs");Near(35,Printer_Plane.Calls[0].Angle,"preserves extra flip rotation");

        item.def.dontPrint=true;Printer_Plane.Calls.Clear();
        Check(!StorageShelfDrawing.PrintItem(layer,item) && Printer_Plane.Calls.Count==0,"honors dontPrint");item.def.dontPrint=false;
        Vector3 center=default(Vector3);object[] args={item,center};
        typeof(StorageShelfDrawing).GetMethod("ItemCenter",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,args);
        Near(print.Center.z,((Vector3)args[1]).z,"DrawPos and labels track the displayed stack");

        MagicStorageMod.Settings.TidySupplyShelf=false;Printer_Plane.Calls.Clear();
        Check(StorageShelfDrawing.PrintItem(layer,item) && Printer_Plane.Calls.Count==0,"off restores native printing");
        Check(StorageShelfDrawing.DrawItem(item,DrawPhase.Draw),"off restores native realtime drawing");
        Check(!StorageShelfDrawing.TryGetShelf(item,out _,out _),"off restores native center and label handling");
        MagicStorageMod.Settings.TidySupplyShelf=true;
        item.Spawned=false;Check(StorageShelfDrawing.PrintItem(layer,item),"carried items use native drawing");item.Spawned=true;
        map.haulDestinationManager.Parent=new object();Check(StorageShelfDrawing.PrintItem(layer,item),"other storage stays native");
        map.haulDestinationManager.Parent=shelf;
        shelf.Selections[1]=null;Check(StorageShelfDrawing.TryGetShelf(item,out _,out slot) && slot>=0 && slot<3,"leftover stacks remain visible");
        shelf.Selections[0]=item.def;shelf.Selections[2]=item.def;
        var second=new Thing { def=item.def,Map=map,Position=shelf.Position };
        var third=new Thing { def=item.def,Map=map,Position=shelf.Position };
        shelf.GetSlotGroup().HeldThings.Add(second);shelf.GetSlotGroup().HeldThings.Add(third);
        Check(StorageShelfDrawing.TryGetShelf(second,out _,out int secondSlot) && secondSlot==0,"same-def second stack uses separate first position");
        Check(StorageShelfDrawing.TryGetShelf(third,out _,out int thirdSlot) && thirdSlot==2,"same-def third stack uses separate third position");
        map.listerThings.AllThings.Add(item);map.listerThings.AllThings.Add(shelf);Find.Maps.Add(map);
        StorageShelfDrawing.RefreshAll();
        Check(map.mapDrawer.Dirty.Count==1,"toggle invalidates shelf meshes without touching other cells");
    }
}
