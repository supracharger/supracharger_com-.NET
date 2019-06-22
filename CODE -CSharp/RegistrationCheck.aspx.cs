using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.Data;

namespace SupraChargerWeb
{
    public partial class RegistrationCheck : System.Web.UI.Page
    {
        string[] _apps = new string[] { "roullettehelper" };
        SqlConnection _sql;
        EncoderLicense _encoder;

        public RegistrationCheck()
        {
            _sql = new SqlConnection(Data._sqlConnection);
            _encoder = new EncoderLicense();
            
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            Response.Clear();                                       //Optional: if we've sent anything before
            Response.ContentType = "text/xml";                      //Must be 'text/xml'
            Response.ContentEncoding = System.Text.Encoding.UTF8;   //We'd like UTF-8
        }

        /// <summary>
        /// Checks Authenication
        /// </summary>
        /// <returns></returns>
        public string Authenication(out string msg)
        {
            msg = "";
            // Wanting a trial
            bool trial = Request.QueryString["type"] != null && Request.QueryString["type"].Trim() == "trial";
            // Check URL var exists
            if (_sql.State == ConnectionState.Closed) _sql.Open();
            if (Request.QueryString["value"] == null || 
                !Val.IsAlphaNumSymbols(Request.QueryString["value"].ToString().Trim(), _encoder._symbols, length:2000))
                return _encoder.Encode("deny", DateTime.Now);
            // Check License
            //string msg = _encoder.Encode("AAAA_Bob", DateTime.Now);
            string encoded = Request.QueryString["value"].ToString().Trim();
            bool check = Check(encoded, DateTime.Now, trial, ref msg);
            if (!check) check = Check(encoded, DateTime.Now.Subtract(new TimeSpan(1, 0, 0)), trial, ref msg);
            _sql.Close();
            // Grant or Deny
            if (check) return _encoder.Encode("grant_authenication_proceed", DateTime.Now);
            if (msg == string.Empty) msg = "Invalid User Data or License.";
            // Return: This way there server does Not have to spend time encoding
            return "DENIED";
        }

        bool Check(string encoded, DateTime T, bool trial, ref string msg)
        {
            try
            {
                // Decode
                string license, email, mac, model;
                string pass = Request.QueryString["pass"];
                bool isStart;
                try { _encoder.DecodeLicense(encoded, T, out license, out email, out model, out mac, out isStart); }
                catch { return false; }     // 2nd run threw with wrong data will throw error
                if (string.IsNullOrEmpty(pass))
                    return false;
                // Format
                license = license.ToLower(); email = email.ToLower();
                pass = Val.Decrypt(pass, "Pass%%Word", useHashing: true).Trim();
                // Val Email
                if (email.Length == 0 || !Val.IsAlphaNumSymbols(email, Val._emailSym))
                    return false;
                // Val License
                if (!trial && (license.Length == 0 || !Val.IsAlphaNum(license)))
                    return false;
                // VAL Model
                if (model.Length == 0 || !Val.IsAlphaNum(model))
                    return false;
                // Val MAC
                if (mac.Length == 0 || !Val.IsAlphaNum(mac, length: 100))
                    return false;
                // Val Password
                if (pass.Length == 0 || !Val.IsAlphaNumSymbols(pass, Val._passSym)) return false;
                if (_sql.State == ConnectionState.Closed) _sql.Open();
                // Sql Get User Info ------------------------------------------
                var com = new SqlCommand("SELECT [id] FROM [UserData] WHERE [Email]=@Email AND [Password]=@Password", _sql);
                com.Parameters.AddWithValue("@Email", email);
                com.Parameters.AddWithValue("@Password", Val.Encrypt(pass));
                var reader = com.ExecuteReader();
                if (!reader.Read())
                { msg = "Please check your USER information because it does Not match our records."; return false; }
                int id = (int)(long)reader["id"];
                reader.Close();
                // --- Free Trial, License is last name -----------------------
                if (trial)
                    return Trial(id, model, ref msg);
                // Sql Get License Info ---------------------------------------
                com = new SqlCommand("SELECT [user],[mac] FROM [License] WHERE [userid]=@userid AND " +
                                    "[model]=@model AND [License]=@License", _sql);
                com.Parameters.AddWithValue("@userid", id);
                com.Parameters.AddWithValue("@model", model.ToUpper());
                com.Parameters.AddWithValue("@License", license.ToUpper());
                reader = com.ExecuteReader();
                if (!reader.Read())         // License Not found
                { msg = "Please check your LICENSE information because it does Not match our records."; return false; }       
                string sqlMac = reader["mac"].ToString().ToLower();
                if (reader.Read()) throw new Exception("ERROR!: More than 1 license found, license: " + license);
                reader.Close();
                // Start of Clients App: Update Mac Address
                if (isStart && mac != sqlMac)
                {
                    // Sql Insert MAC Address
                    com = new SqlCommand("UPDATE [License] SET [mac]=@mac WHERE [userid]=@userid AND [License]=@License", _sql);
                    com.Parameters.AddWithValue("@mac", mac);
                    com.Parameters.AddWithValue("@userid", id);
                    com.Parameters.AddWithValue("@License", license);
                    if (com.ExecuteNonQuery() != 1) throw new Exception("ERROR!: Could Not Update License MAC Address.");
                }
                // Client Periodic Checks, Check MAC Address
                else if (!isStart && mac != sqlMac)
                {
                    msg = "Another computer is running this application using your account License. Please consider buying additional licenses for that computer.";
                    return false;
                }
                // Return True
                return true;
            }
            catch (Exception e)
            { msg = Logger._erMsg; Logger.Log(e); return false; }
        }

        bool Trial(int userid, string model, ref string msg)
        {
            model = model.ToUpper();
            // Check FreeTrails Table
            var com = new SqlCommand("SELECT [end] FROM [Trials] WHERE [userid]=@userid AND [model]=@model", _sql);
            com.Parameters.AddWithValue("@userid", userid);
            com.Parameters.AddWithValue("@model", model);
            var reader = com.ExecuteReader();
            // Already Registered Trial
            if (reader.Read())
            {
                DateTime end = DateTime.Parse(reader["end"].ToString());
                reader.Close();
                bool active = end.Subtract(DateTime.Now).TotalDays > 0;
                if (!active) msg = "Sorry, your Free Trial has ended. Thank you for using our software! Please consider the Full Version.";
                return active;
            }
            // Create Trial
            else
            {
                reader.Close();
                // Start Free Trial
                com = new SqlCommand("INSERT INTO [Trials] ([userid],[model],[end]) VALUES (@userid,@model,@end)", _sql);
                com.Parameters.AddWithValue("@userid", userid);
                com.Parameters.AddWithValue("@model", model);
                com.Parameters.AddWithValue("@end", DateTime.Now.AddDays(7).ToString());
                if (com.ExecuteNonQuery() != 1) throw new Exception($"ERROR!: Could Not add FreeTrail. userid: {userid} {model}");
                return true;
            }
        }
    }
}