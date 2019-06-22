using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace SupraChargerWeb
{
    public struct Info
    {
        public int id { get; private set; }
        public string First { get; private set; }
        public string Email { get; private set; }
        public Info(int id, string First, string Email)
        {
            this.id = id;
            this.First = First;
            this.Email = Email;
        }
        public Info(object id, object First, object Email)
        {
            this.id = Convert.ToInt32(id);
            this.First = First.ToString();
            this.Email = Email.ToString();
        }

        public override string ToString()
        {
            return $"{First} {Email} {id}";
        }
    }
}