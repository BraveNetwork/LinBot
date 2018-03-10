using System;

namespace NadekoBot.Core.Services.Database.Models
{
    public class WaifuItem : DbEntity
    {
        public string ItemEmoji { get; set; }
        public int Price { get; set; }
        public ItemName Item { get; set; }

        public enum ItemName
        {
            Cookie,
            Rose,
            LoveLetter,
            Chocolate,
            Rice,
            MovieTicket,
            Book,
            Lipstick,
            Laptop,
            Violin,
            Ring,
            Helicopter,
            Iphone, //4000
            Cat, //2000
            Dog, //2001
            Lollipop, //30
            Purse, //3500
            Sushi, //300
            Icecream, //200
            Piano, //8000
            Yacht, //12000
            Car, //9000
            House, //15000
            Spaceship, //30000
            Beer, //75
            Pizza, //150
            Milk, //125
            Bread, //100
            Taco, //300
            Bento, //1200
            Potato, //20
            Moon, //100k
            Panda, //5k
            Cake, //2000
            Watermelon,//1000
            Dress, //4000
        }

        public WaifuItem()
        {

        }

        public WaifuItem(string itemEmoji, int price, ItemName item)
        {
            ItemEmoji = itemEmoji;
            Price = price;
            Item = item;
        }

        public static WaifuItem GetItem(ItemName itemName, int mult)
        {
            WaifuItem wi;
            switch (itemName)
            {
                case ItemName.Potato:
                    wi = new WaifuItem("🥔", 1, itemName);
                    break;
                case ItemName.Cookie:
                    wi = new WaifuItem("🍪", 1, itemName);
					break;
                case ItemName.Rose:
                    wi = new WaifuItem("🌹", 5, itemName);
					break;
                case ItemName.LoveLetter:
                    wi = new WaifuItem("💌", 10, itemName);
					break;
                case ItemName.Chocolate:
                    wi = new WaifuItem("🍫", 20, itemName);
					break;
                case ItemName.Rice:
                    wi = new WaifuItem("🍚", 40, itemName);
					break;
                case ItemName.MovieTicket:
                    wi = new WaifuItem("🎟", 80, itemName);
					break;
                case ItemName.Book:
                    wi = new WaifuItem("📔", 150, itemName);
					break;
                case ItemName.Lipstick:
                    wi = new WaifuItem("💄", 300, itemName);
					break;
                case ItemName.Laptop:
                    wi = new WaifuItem("💻", 500, itemName);
					break;
                case ItemName.Violin:
                    wi = new WaifuItem("🎻", 750, itemName);
					break;
                case ItemName.Ring:
                    wi = new WaifuItem("💍", 1000, itemName);
					break;
                case ItemName.Helicopter:
                    wi = new WaifuItem("🚁", 2000, itemName);
					break;
                case ItemName.Iphone:
                    wi = new WaifuItem("📱", 400, itemName);
					break;
                case ItemName.Cat:
                    wi = new WaifuItem("🐱", 200, itemName);
					break;
                case ItemName.Dog:
                    wi = new WaifuItem("🐶", 201, itemName);
					break;
                case ItemName.Lollipop:
                    wi = new WaifuItem("🍭", 3, itemName);
					break;
                case ItemName.Purse:
                    wi = new WaifuItem("👛", 350, itemName);
					break;
                case ItemName.Sushi:
                    wi = new WaifuItem("🍣", 30, itemName);
					break;
                case ItemName.Icecream:
                    wi = new WaifuItem("🍦", 20, itemName);
					break;
                case ItemName.Piano:
                    wi = new WaifuItem("🎹", 800, itemName);
					break;
                case ItemName.Yacht:
                    wi = new WaifuItem("🛳", 1200, itemName);
					break;
                case ItemName.Car:
                    wi = new WaifuItem("🚗", 900, itemName);
					break;
                case ItemName.House:
                    wi = new WaifuItem("🏠", 1500, itemName);
					break;
                case ItemName.Spaceship:
                    wi = new WaifuItem("🚀", 3000, itemName);
					break;
                case ItemName.Beer:
                    wi = new WaifuItem("🍺", 7, itemName);
					break;
                case ItemName.Pizza:
                    wi = new WaifuItem("🍕", 15, itemName);
					break;
                case ItemName.Milk:
                    wi = new WaifuItem("🥛", 12, itemName);
					break;
                case ItemName.Bread:
                    wi = new WaifuItem("🥖", 2, itemName);
                    break;
                case ItemName.Taco:
                    wi = new WaifuItem("🌮", 9, itemName);
                    break;
                case ItemName.Watermelon:
                    wi = new WaifuItem("🍉", 50, itemName);
                    break;
                case ItemName.Bento:
                    wi = new WaifuItem("🍱", 60, itemName);
                    break;
                case ItemName.Cake:
                    wi = new WaifuItem("🍰", 100, itemName);
                    break;
                case ItemName.Panda:
                    wi = new WaifuItem("🐼", 250, itemName);
                    break;
                case ItemName.Dress:
                    wi = new WaifuItem("👗", 450, itemName);
                    break;
                case ItemName.Moon:
                    wi = new WaifuItem("🌕", 5000, itemName);
                    break;
                default:
                    throw new ArgumentException(nameof(itemName));
            }
            wi.Price = wi.Price * mult;
            return wi;
        }
    }
}


/*
🍪 Cookie 10
🌹  Rose 50
💌 Love Letter 100
🍫  Chocolate 200
🍚 Rice 400
🎟  Movie Ticket 800
📔 Book 1.5k
💄  Lipstick 3k
💻 Laptop 5k
🎻 Violin 7.5k
💍 Ring 10k
*/
