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
    public partial class EditUserInfo : System.Web.UI.Page
    {
        SqlConnection _sql;

        public EditUserInfo() { _sql = new SqlConnection(Data._sqlConnection); }

        protected void Page_Load(object sender, EventArgs e)
        {
            if (Session["data"] == null)    // Exit if Not Auth.
                Response.Redirect("~/index.aspx");
            // Browser Back Navigation causes issue: This is why reload.
            if (Request.QueryString["msg"] != null)
            { ClearError(); lbSuccess.Text = Request.QueryString["msg"].ToString(); }

            if (!IsPostBack)
            {
                FillUserData();  // Fill Form
            }
        }

        protected void btnUpdate_Click(object sender, EventArgs e)
        {
            string first, last, email;
            try
            {
                first = txFirst.Text.Trim();
                last = txLast.Text.Trim();
                email = txEmail.Text.Trim().ToLower();
                // QueryString Info for Primary Key & Email
                Info I = (Info)Session["data"];
                // Validation
                if (!Valid(first, last, email, txEmailConfirm.Text.Trim().ToLower())) return;
                // VAL: SQL Email Already in System
                if (_sql.State == ConnectionState.Closed) _sql.Open();
                var com = new SqlCommand("SELECT count([id]) FROM [UserData] WHERE [Email]=@email AND [id]<>@id", _sql);
                com.Parameters.AddWithValue("@email", email);
                com.Parameters.AddWithValue("@id", I.id);
                if ((int)com.ExecuteScalar() > 0)
                { lbErrorMsg.Text = "The Specified Email is already registered in the system."; return; }
                // Val ReCaptcha
                if (!CheckRecaptcha(email)) return;
                // SQL Update with new Data
                if (_sql.State == ConnectionState.Closed) _sql.Open();
                string cm = "UPDATE [UserData] SET [First]=@First, [Last]=@Last, [Email]=@Email WHERE [id]=@id";
                com = new SqlCommand(cm, _sql);
                com.Parameters.AddWithValue("@First", Val.UpperFirst(first));
                com.Parameters.AddWithValue("@Last", Val.UpperFirst(last));
                com.Parameters.AddWithValue("@Email", email);
                com.Parameters.AddWithValue("@id", I.id);
                if (com.ExecuteNonQuery() == 0) throw new Exception("Could Not update user data. email: " + I.Email);
                // Update Login
                Session["data"] = new Info(I.id, first, email);
                // Disable
                btnUpdate.Enabled = txEmailConfirm.Enabled = false;
                txEmailConfirm.Text = "";
                // Success User
                lbSuccess.Text = "Information Updated Successfully!";
            }
            catch (Exception ex) // Exit
            { _sql.Close(); lbErrorMsg.Text = Logger._erMsg; Logger.Log(ex); return; }
            _sql.Close();

            // Browser Back Navigation causes issue: This is why reload.
            Response.Redirect("~/EditUserInfo.aspx?msg=" + Server.UrlEncode(lbSuccess.Text));
        }

        void FillUserData()
        {
            try
            {
                Info I = (Info)Session["data"];
                if (_sql.State == ConnectionState.Closed) _sql.Open();
                // Get Last Name from SQL
                var com = new SqlCommand("SELECT [Last] FROM [UserData] WHERE [id]=@id", _sql);
                com.Parameters.AddWithValue("@id", I.id);
                var reader = com.ExecuteReader();
                if (!reader.Read()) throw new Exception("Could Not find user data. email: " + I.Email);
                string last = reader["Last"].ToString();   // Get Last Name
                if (reader.Read()) throw new Exception("User has more than 1 row. email: " + I.Email);
                reader.Close();
                // Fill Data
                txFirst.Text = I.First;
                txLast.Text = last;
                txEmail.Text = I.Email;
            }
            catch (Exception ex) // Exit
            { _sql.Close(); lbErrorMsg.Text = Logger._erMsg; Logger.Log(ex); return; }
            _sql.Close();
        }

        bool Valid(string first, string last, string email, string emailConfirm)
        {
            ClearError();
            // First
            if (first == string.Empty)
            { lbErrorMsg.Text = "First Name can Not be Blank."; lbEr0.Text = "*"; return false; }
            else if (!Val.IsAlpha(first))
            { lbErrorMsg.Text = "First Name Only letters please."; lbEr0.Text = "*"; return false; }
            // Last
            if (last == string.Empty)
            { lbErrorMsg.Text = "Last Name can Not be Blank."; lbEr1.Text = "*"; return false; }
            else if (!Val.IsAlpha(last))
            { lbErrorMsg.Text = "Last Name Only letters please."; lbEr1.Text = "*"; return false; }
            // Email
            if (email == string.Empty)
            { lbErrorMsg.Text = "Email can Not be Blank."; lbEr2.Text = "*"; return false; }
            else if (!email.Contains('.') || !email.Contains('@') || !Val.IsAlphaNumSymbols(email, Val._emailSym))
            { lbErrorMsg.Text = "Invalid Email."; lbEr2.Text = "*"; return false; }
            else if (email != emailConfirm)     // Email Equal
            { lbErrorMsg.Text = "Your Email & Confirm_Email are Not Equal."; lbEr2.Text = lbEr3.Text = "*"; return false; }
            // Pass Validation
            return true;
        }

        bool CheckRecaptcha(string email)
        {
            Recaptcha.Token token;
            // Val ReCaptchaV2 Checkbox
            if (!Recaptcha.IsValidV3(Request.Form["g-recaptcha-response"], false, out token))
            {
                lbErrorMsg.Text = "Please check the ReCaptcha checkbox at the bottom and follow the prompt if need be. Additionally, check the checkbox before clicking the submit button.";
                return false;
            }
            // Return true
            return true;
        }

        // Gets the Keys by Host LocalHost or Supracharger.com
        protected Recaptcha.Version RecaptchaVersion(bool isV3) { return Recaptcha.GetVersionHost(HttpContext.Current.Request, isV3); }

        void ClearError()
        {
            lbErrorMsg.Text = lbSuccess.Text = "";
            lbEr0.Text = lbEr1.Text = lbEr2.Text = lbEr3.Text = "";
        }
    }
}