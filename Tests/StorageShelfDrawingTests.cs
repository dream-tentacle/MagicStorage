using System;
using System.Reflection;
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
        try { Layout(); DynamicLayout(); Routing(); DynamicRouting(); Console.WriteLine("PASS: "+checks+" shelf layout/drawing assertions (renderer boundaries stubbed).");return 0; }
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
