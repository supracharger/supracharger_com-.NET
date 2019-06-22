using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.Data;

// https://stackoverflow.com/questions/27764692/validating-recaptcha-2-no-captcha-recaptcha-in-asp-nets-server-side
// https://stackoverflow.com/questions/53590011/how-to-implement-recaptcha-v3-in-asp-net
// https://stackoverflow.com/questions/51507695/google-recaptcha-v3-example-demo

namespace SupraChargerWeb
{
    public partial class Login : System.Web.UI.Page
    {
        SqlConnection _sql;

        public Login () { _sql = new SqlConnection(Data._sqlConnection);}
        
        protected void Page_Load(object sender, EventArgs e)
        {
            // Redirect
            if (Session["Data"] != null) Response.Redirect("~/index.aspx");
            if (!IsPostBack)
            {
                // Message from previous Page
                if (Request.QueryString["msg"] != null)
                    lbMsg.Text = Request.QueryString["msg"].ToString().Replace("[[*n*]]", "<br />");
                // Previous Page Post
                if (Request.Form.AllKeys.Length > 0 && string.IsNullOrEmpty(txEmail.Text) && string.IsNullOrEmpty(txPassword.Text))
                {
                    string email = Request.Form["txEmail"], password = Request.Form["txPassword"];
                    if (email != null && password != null)      // Correct Fields Posted
                    {
                        txEmail.Text = email;       txPassword.Text = password; // Need to set password cuz btnLogin_Click()
                        btnLogin_Click(null, null);     // Run the Submit Operation
                    }
                }
            }
        }

        protected void btnLogin_Click(object sender, EventArgs e)
        {
            try
            {
                string email, password;
                email = txEmail.Text.Trim().ToLower(); password = txPassword.Text.Trim();
                txPassword.Text = "";
                Clear();
                // Val Email
                if (email == string.Empty || !Val.IsAlphaNumSymbols(email, Val._emailSym))
                { lbErrorMsg.Text = "Invalid Email."; return; }
                // Val Password
                if (password.Length < 5 || !Val.IsAlphaNumSymbols(password, Val._passSym, length: 30))
                { lbErrorMsg.Text = "Invalid Password."; return; }
                // SQL Look up
                string oldPass = password;
                password = Val.Encrypt(password);
                if (_sql.State == ConnectionState.Closed) _sql.Open();
                var com = new SqlCommand("SELECT [id], [First], [Email] FROM [UserData] WHERE [Email]=@Email AND [Password]=@Password", _sql);
                com.Parameters.AddWithValue("@Email", email);
                com.Parameters.AddWithValue("@Password", password);
                var reader = com.ExecuteReader();
                if (!reader.Read())
                { lbErrorMsg.Text = "Error Email or Password do Not match."; return; }
                // Save Session Info
                Info info;
                Session["data"] = info = new Info(reader["id"], reader["First"], reader["Email"]);
                reader.Close();
                // Check ReCaptcha & Possibly Save HumanScore
                double humanScore;
                if (CheckRecaptcha(info.Email, info, out humanScore) && humanScore < 0.40) // Has to be this way cuz: Posting to this Form will make success==false
                { Session["data"] = null; lbErrorMsg.Text = "Whoops! It looks look you are being classified as a robot."; return; }
                // Remember me login
                //if (ckRemember.Checked)
                //{
                //    string val = Val.Encrypt(email + "^*^" + oldPass, encryptionKey: Val._remLoginSalt);
                //    var cookie = new Cookie("data", $"\"{val}\"");
                //    cookie.Domain = ".localhost";
                //    cookie.Expires = DateTime.Now.AddDays(30);
                //    new CookieContainer().Add(cookie);
                //}
            }
            catch (Exception ex)    // Exit Safer
            { _sql.Close(); Session["data"] = null; lbErrorMsg.Text = Logger._erMsg; Logger.Log(ex); return; }
            _sql.Close();

            // Redirect Url Specified
            string url = Request.QueryString["url"];
            if (!string.IsNullOrEmpty(url) && !url.ToLower().Contains("logout"))
                Response.Redirect(url);
            // Redirect Default
            else Response.Redirect("~/index.aspx");
        }

        bool CheckRecaptcha(string email, Info info, out double score)
        {
            score = 0;
            Recaptcha.Token token;
            // Val ReCaptchaV3: Since 1st login Posts to this page your can't use ReCaptcha
            if (!Recaptcha.IsValidV3(Request.Form["g-recaptcha-responsev3"], true, out token))
                return false;   // Can't throw error (above)
            score = token.Score;

            // SQL Save Human Score
            if (_sql.State == ConnectionState.Closed) _sql.Open();
            // SQL Make sure there is a day change to submit ----------------------
            var com = new SqlCommand("SELECT TOP 1 [dateTime] FROM [HumanScore] WHERE [userid]=@userid ORDER BY [dateTime] DESC", _sql);
            com.Parameters.AddWithValue("@userid", info.id);
            var reader = com.ExecuteReader();
            bool add;
            // Make sure it is a different day
            if (reader.Read())
                add = Data.DateTimeValue(DateTime.Now) != (int)reader["dateTime"];
            else add = true;
            reader.Close();
            // SQL: Insert HumanScore if true --------------------------------------
            if (add)
            {
                com = new SqlCommand("INSERT INTO [HumanScore] ([userid],[humanScore],[dateTime],[page]) VALUES (@userid,@humanScore,@dateTime,@page)", _sql);
                com.Parameters.AddWithValue("@userid", info.id);
                com.Parameters.AddWithValue("@humanScore", (float)token.Score);
                com.Parameters.AddWithValue("@dateTime", Data.DateTimeValue(DateTime.Now));
                com.Parameters.AddWithValue("@page", "Login");
                if (com.ExecuteNonQuery() == 0) throw new Exception("Could Not insert HumanScore. email: " + info.Email);
            }
            // SQL: Delete Older data of 50+ ---------------------------------------
            com = new SqlCommand("DELETE FROM [HumanScore] WHERE [id] IN " +
                    "(SELECT [id] FROM (SELECT [id],ROW_NUMBER() OVER(ORDER BY [dateTime] DESC) AS rw FROM [HumanScore] WHERE [userid]=@userid)" +
                    "res WHERE res.rw > @max)", _sql);
            com.Parameters.AddWithValue("@userid", info.id);
            com.Parameters.AddWithValue("@max", 5);
            com.ExecuteNonQuery();      // Ok, if No rows updated
            // Return
            return true;
        }

        // Gets the Keys by Host LocalHost or Supracharger.com
        protected Recaptcha.Version RecaptchaVersion(bool isV3) { return Recaptcha.GetVersionHost(HttpContext.Current.Request, isV3); }

        void Clear() { lbErrorMsg.Text = lbMsg.Text = ""; }
    }
}