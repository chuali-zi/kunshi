using System.Collections.Generic;
using ConGame.Domain.Interaction;

namespace ConGame.Runtime.Data
{

    public readonly struct ItemSeed
    {
        public ItemSeed(string id, string displayName, InteractionVerb verbs, ItemCategory categories, ContentStatus status)
        {
            Id = id;
            DisplayName = displayName;
            Verbs = verbs;
            Categories = categories;
            Status = status;
        }

        public string Id { get; }
        public string DisplayName { get; }
        public InteractionVerb Verbs { get; }
        public ItemCategory Categories { get; }
        public ContentStatus Status { get; }
    }

    public static class ItemSeedLibrary
    {
        private const InteractionVerb Inspect = InteractionVerb.Inspect;

        public static IReadOnlyList<ItemSeed> All { get; } = new[]
        {
            new ItemSeed("sofa_awaken", "苏醒扶手椅", Inspect | InteractionVerb.Hide, ItemCategory.None, ContentStatus.Implemented),
            new ItemSeed("blanket", "被子", Inspect | InteractionVerb.PickUp | InteractionVerb.Use | InteractionVerb.Throw | InteractionVerb.Hide | InteractionVerb.Place, ItemCategory.Small | ItemCategory.Throwable | ItemCategory.TripTrap, ContentStatus.Implemented),
            new ItemSeed("bed", "床", Inspect | InteractionVerb.Use | InteractionVerb.Hide, ItemCategory.None, ContentStatus.Implemented),
            new ItemSeed("wood_chair", "木椅", Inspect | InteractionVerb.Carry | InteractionVerb.Throw | InteractionVerb.Swing | InteractionVerb.Use | InteractionVerb.Place, ItemCategory.Heavy | ItemCategory.Throwable | ItemCategory.Weapon | ItemCategory.DoorBlocker | ItemCategory.EndingTool, ContentStatus.Implemented),
            new ItemSeed("bookcase", "书柜", Inspect | InteractionVerb.Use | InteractionVerb.Hide, ItemCategory.Heavy | ItemCategory.DoorBlocker, ContentStatus.Implemented),
            new ItemSeed("baseball_bat", "棒球棍", Inspect | InteractionVerb.PickUp | InteractionVerb.Throw | InteractionVerb.Swing, ItemCategory.Small | ItemCategory.Throwable | ItemCategory.Weapon | ItemCategory.EndingTool, ContentStatus.Implemented),
            new ItemSeed("room_door", "房门", Inspect | InteractionVerb.Use | InteractionVerb.Place, ItemCategory.None, ContentStatus.Implemented),
            new ItemSeed("ceiling_light", "顶灯", Inspect, ItemCategory.LightSource, ContentStatus.Implemented),
            new ItemSeed("table_lamp", "台灯", Inspect | InteractionVerb.PickUp | InteractionVerb.Use | InteractionVerb.Throw, ItemCategory.Small | ItemCategory.Throwable | ItemCategory.LightSource, ContentStatus.Implemented),
            new ItemSeed("walls", "墙壁", Inspect | InteractionVerb.Use, ItemCategory.None, ContentStatus.Implemented),
            new ItemSeed("crt_tv", "CRT 电视", Inspect | InteractionVerb.Use, ItemCategory.None, ContentStatus.Implemented),
            new ItemSeed("remote", "遥控器", Inspect | InteractionVerb.PickUp | InteractionVerb.Use | InteractionVerb.Throw, ItemCategory.Small | ItemCategory.Throwable, ContentStatus.Implemented),
            new ItemSeed("pistol_main", "主手枪", Inspect | InteractionVerb.PickUp | InteractionVerb.Use | InteractionVerb.Throw, ItemCategory.Small | ItemCategory.Throwable | ItemCategory.Weapon | ItemCategory.Firearm, ContentStatus.Implemented),
            new ItemSeed("pistol_backup", "备用手枪", InteractionVerb.None, ItemCategory.Firearm, ContentStatus.Implemented),
            new ItemSeed("flashlight", "战术手电", Inspect | InteractionVerb.PickUp | InteractionVerb.Use | InteractionVerb.Throw, ItemCategory.Small | ItemCategory.Throwable | ItemCategory.LightSource, ContentStatus.Implemented),
            new ItemSeed("killer_coat", "风衣", Inspect, ItemCategory.None, ContentStatus.Implemented),
            new ItemSeed("dresser", "抽屉柜", Inspect | InteractionVerb.Use | InteractionVerb.Carry | InteractionVerb.Place, ItemCategory.Heavy | ItemCategory.DoorBlocker, ContentStatus.Implemented),
            new ItemSeed("books", "书本", Inspect | InteractionVerb.PickUp | InteractionVerb.Use | InteractionVerb.Throw, ItemCategory.Small | ItemCategory.Throwable, ContentStatus.Implemented),
            new ItemSeed("vase", "花瓶", Inspect | InteractionVerb.PickUp | InteractionVerb.Throw, ItemCategory.Small | ItemCategory.Throwable, ContentStatus.Implemented),
            new ItemSeed("boarded_window", "封死窗户", Inspect | InteractionVerb.Use, ItemCategory.None, ContentStatus.Implemented),
            new ItemSeed("rug", "地毯", Inspect | InteractionVerb.Use, ItemCategory.TripTrap, ContentStatus.Implemented),
            new ItemSeed("wall_clock", "挂钟", Inspect | InteractionVerb.PickUp | InteractionVerb.Throw, ItemCategory.Small | ItemCategory.Throwable, ContentStatus.Implemented),
            new ItemSeed("mirror", "镜子", Inspect | InteractionVerb.Use, ItemCategory.None, ContentStatus.Implemented)
        };
    }
}
