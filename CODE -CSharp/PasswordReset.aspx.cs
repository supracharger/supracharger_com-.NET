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
    public partial class PasswordReset : System.Web.UI.Page
    {
        SqlConnection _sql;

        public PasswordReset()
        {
            _sql = new SqlConnection(Data._sqlConnection);
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            // If Comming from MyAccount/CreateNewPassword
            if (Session["data"] != null)
                txEmail.Text = ((Info)Session["data"]).Email;
        }

        protected void btnReset_Click(object sender, EventArgs e)
        {
            try
            {
                Clear();
                // Format
                string email = txEmail.Text.Trim().ToLower();
                // Val Email string
                if (email.Length == 0)
                { lbErrorMsg.Text = "Email can Not be Blank."; return; }
                else if (!email.Contains('.') || !email.Contains('@') || !Val.IsAlphaNumSymbols(email, Val._emailSym))
                { lbErrorMsg.Text = "ERROR!: Invalid Email."; return; }

                // Check if in SQL
                if (_sql.State == ConnectionState.Closed) _sql.Open();
                var com = new SqlCommand("SELECT [id],[Email],[First] FROM [UserData] WHERE [Email]=@Email", _sql);
                com.Parameters.AddWithValue("@Email", email);
                var reader = com.ExecuteReader();
                if (!reader.Read())
                { lbErrorMsg.Text = "There is No account registered with the specified email."; return; }
                // Get Info Not for Session["data"], but For Recapta SQL Store
                Info info = new Info(reader["id"], reader["First"], reader["Email"]);
                if (reader.Read()) throw new Exception("ERROR!: More than 1 entry found for email: " + email);
                reader.Close();
                // ReCaptcha: If Info Class not found, will only run the check box
                double score;
                if (!CheckRecaptcha(email, info, out score))
                    return; // Allow it to display Error Messages

                // Send Email to Reset Password
                SendEmailReset(email);
                // Send email to Reset Password
                btnReset.Enabled = txEmail.Enabled = false;       // Disable submit button & Text
                lbMsg.Text = "An Email has been sent to the specified Address. Please follow the link in your email to reset your password. " +
                    "<br />Authorization Code will Expire in 20 minutes.";
                // Logout if Logged in: Since Password Reset Navigates to this Page
                if (Session["data"] != null)
                    Session["data"] = null;
            }
            catch (Exception ex)
            { _sql.Close(); lbErrorMsg.Text = Logger._erMsg; Logger.Log(ex); return; }
            _sql.Close();
        }

        void SendEmailReset(string email)
        {
            // SQL & AuthCode
            // Get Auth. Code
            string code = Data.RandAlphaNum();
            // SQL Check for another row with user
            if (_sql.State == ConnectionState.Closed) _sql.Open();
            var com = new SqlCommand("SELECT count(*) FROM [PasswordReset] WHERE [user]=@user", _sql);
            com.Parameters.AddWithValue("@user", email);
            // Delete those Rows
            if ((int)com.ExecuteScalar() > 0)
            {
                com = new SqlCommand("DELETE FROM [PasswordReset] WHERE [user]=@user", _sql);
                com.Parameters.AddWithValue("@user", email);
                if (com.ExecuteNonQuery() == 0) throw new Exception("Could Not delete from Table. email: " + email);
            }
            // Insert AuthCode into Row
            com = new SqlCommand("INSERT INTO [PasswordReset] ([user], [code], [expiry]) VALUES (@user, @code, @expiry)", _sql);
            com.Parameters.AddWithValue("@user", email);
            com.Parameters.AddWithValue("@code", code);
            com.Parameters.AddWithValue("@expiry", DateTime.Now.AddMinutes(20).ToString());
            if (com.ExecuteNonQuery() == 0) throw new Exception("Could not insert row. email: " + email);

            // URL for the user to Nav. to
            string url = Data.UrlRemove(HttpContext.Current.Request.Url.AbsoluteUri);
            url += "/PassResetVal.aspx?code=" + Server.UrlEncode(code);
            // Send Email
            string title = "Reset Password Request";
            Data.SendEmail(title, EmailBody(title, url), email, isBodyHtml: true);
        }

        bool CheckRecaptcha(string email, Info info, out double score)
        {
            score = 0;
            Recaptcha.Token token;
            // Val ReCaptchaV2-- Checkbox
            if (!Recaptcha.IsValidV3(Request.Form["g-recaptcha-response"], false, out token))
            {
                lbErrorMsg.Text = "Please check the ReCaptcha checkbox at the bottom and follow the prompt if need be. Additionally, check the checkbox before clicking the submit button.";
                return false;
            }

            // Val ReCaptchaV3-- Ok to throw Error
            if (!Recaptcha.IsValidV3(Request.Form["g-recaptcha-responsev3"], true, out token))
                throw new Exception("ReCaptchaV3 was unsuccessful. email: " + email);
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
                com.Parameters.AddWithValue("@page", "PasswordReset");
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

        // Email Body
        string EmailBody(string title, string url)
        {
            string html = System.IO.File.ReadAllText(Data._otherFiles + "\\email.html");
            string body = "<p>Please <a href=\"" + url + "\">CLICK HERE</a> to Reset your Password.<p>" +
                "<p><i>Or Copy & Paste the URL into your Browser:</i><br />" +
                 Server.HtmlEncode(url) + "</p>" +
                 "<p>Authorization Code will Expire in 20 minutes.</p>" +
                 "<p>Thank you for using SupraCharger.com<br />" +
                 "NOTE: If this was Not you who asked to Reset your Password, please contact support to help solve the issue.</p>";
            // Insert Title & Body into HTML Email
            return html.Replace("[[*header*]]", title)
                    .Replace("[[*body*]]", body);
        }

        void Clear() { lbErrorMsg.Text = lbMsg.Text = ""; }
    }
}