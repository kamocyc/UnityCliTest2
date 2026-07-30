namespace FormosaExpress.City
{
    /// <summary>Flavour text pools. Kept separate so the generator stays readable.</summary>
    public static class CityNames
    {
        public static readonly string[] FoodShops =
        {
            "Taipei Cafe", "Ah-Po Braised", "Boba Palace", "Shilin Fried Chicken",
            "Formosa Noodle", "Jade Dumpling", "Ximen Bakery", "Beitou Hot Pot",
            "Tamsui Fish Ball", "Sun Moon Tea", "Golden Pork Rice", "Night Market Grill",
            "Lucky Scallion Pancake", "Maokong Tea House", "Keelung Seafood", "Zhongshan Sushi",
            "Da'an Curry", "Longshan Vegetarian", "Pearl Milk Lab", "Bitan Beef Noodle",
            "Uncle Wu Snacks", "Sanchong Breakfast", "Neon Ramen", "Sugar Cane Stand",
            "Auntie Kuo Dumplings", "Lantern Street BBQ", "Double Happiness Buns", "Wanhua Wonton"
        };

        public static readonly string[] Residences =
        {
            "Chen Residence", "Lin Family, 4F", "Apt 12B", "Ms. Kuo, 3F",
            "Wang Household", "Hsu Residence, 6F", "Yang Family", "Apt 8A",
            "Tsai Residence", "Old Town Flats", "Sunrise Apartments", "Jasmine Court",
            "Peony Building, 2F", "Bamboo Heights", "Riverside Flats, 5F", "Mr. Ho, 7F",
            "Cheng Household", "Camphor Lane 14", "Plum Blossom, 3F", "Pei Residence"
        };

        public static readonly string[] Offices =
        {
            "Formosa Tech, 9F", "Jade Trading Co.", "Studio Seven", "Taipei Print Works",
            "Blue Whale Design", "Hsinchu Semis Office", "Lucky Star Logistics",
            "Cloud Nine Media", "Ministry Annex", "Orchid Law Firm", "Neon Games Studio",
            "Dragon Bank, 12F"
        };

        public static readonly string[] Landmarks =
        {
            "Temple Gate", "Night Market Arch", "Old Well Plaza", "Bus Depot",
            "Community Centre", "Riverside Shrine", "Public Bathhouse"
        };

        public static readonly string[] Dishes =
        {
            "Bubble Tea", "Braised Pork Rice", "Beef Noodle Soup", "Xiao Long Bao",
            "Fried Chicken Cutlet", "Scallion Pancake", "Oyster Omelette", "Pineapple Cake",
            "Stinky Tofu", "Mango Shaved Ice", "Sesame Noodles", "Pork Buns",
            "Winter Melon Tea", "Lu Rou Fan", "Wonton Soup", "Sweet Potato Balls",
            "Salt & Pepper Squid", "Coffin Bread", "Bamboo Rice", "Taro Milk"
        };

        /// <summary>Faux-signage word lengths, in characters, used by the sign glyph generator.</summary>
        public static readonly int[] SignLengths = { 2, 2, 3, 3, 4, 2, 3 };
    }
}
