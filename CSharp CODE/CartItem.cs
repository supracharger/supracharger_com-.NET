using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace SupraChargerWeb
{
    public class CartItem
    {
        static List<string> _allModels = new[] { "RLA", "MCP" }.ToList();
        // Object Members
        public string _model { get; private set; }
        public int _num { get; private set; }
        public double _price {
            get
            {
                if (_priceValue <= 0) throw new Exception("ERROR!: _price is not set _model: " + _model);
                return _priceValue;
            }
        }
        double _priceValue = -1;            // Default No-value

        // Clones: Put here so if any new vars Clone() will be updated also.
        public CartItem Clone()
        {
            CartItem C = new CartItem(_model);
            C._num = _num;
            C._priceValue = _priceValue;
            return C;
        }

        public CartItem(string model)
        {
            _model = model;
            _num = 1;
            if (_allModels.FindIndex(m => m == model) < 0) throw new Exception("ERROR!: model is Not found in List, model: " + model);
        }

        public void SetPrice(double price) { _priceValue = price; }
        public void SetNum(int num)
        {
            if (num <= 0) throw new Exception("num <= 0");
            _num = num;
        }
        public override string ToString() { return $"{_model} X{_num}"; }

        /// <summary>
        /// Adds 1 or Creates Item, Adds New Items, items==null: creates list
        /// </summary>
        /// <param name="items"></param>
        /// <param name="model"></param>
        /// <param name="add"></param>
        /// <returns></returns>
        public static List<CartItem> Add(List<CartItem> items, string model)
        {
            // Create List if null
            if (items == null) items = new List<CartItem>();
            // Find Pos of Item, if No item add it
            int pos = items.FindIndex(m => m._model == model);
            // Add New Item to Cart & 'pos'==index
            if (pos < 0)
                items.Add(new CartItem(model)); // Constructor makes _num = 1
            // Add +1 to Item
            else
                items[pos]._num++;
            // Return 
            return items;
        }

        /// <summary>
        /// Removes Item Completely no matter how many items there are.
        /// </summary>
        /// <param name="items"></param>
        /// <param name="model"></param>
        /// <returns></returns>
        public static List<CartItem> RemoveItem(List<CartItem> items, string model)
        {
            if (items == null) return items; 
            // Delete Item if Found
            int pos = items.FindIndex(m => m._model == model);
            if (pos >= 0)
                items.RemoveAt(pos);
            // See if need to Clear Cart Entirly
            if (items.Count == 0)
                return null;
            // Return Object
            return items;
        }
    }
}