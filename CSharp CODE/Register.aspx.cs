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
    public partial class Register : System.Web.UI.Page
    {
        SqlConnection _sql;

        public Register()
        {
            _sql = new SqlConnection(Data._sqlConnection);
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            // Browser Back Navigation causes issue: This is why reload.
            if (Request.QueryString["msg"] != null)
            { ClearError(); lbSuccess.Text = Request.QueryString["msg"].ToString(); }
        }

        bool Valid(string first, string last, string email, string emailConfirm,
                   string password, string passConfirm)
        {
            ClearError();
            // First
            if (first == string.Empty)
            { lbErrorMsg.Text = "ERROR!: First Name is Blank."; lbEr0.Text = "*"; return false; }
            else if (!Val.IsAlpha(first))
            { lbErrorMsg.Text = "ERROR!: First Name Only letters please."; lbEr0.Text = "*"; return false; }
            // Last
            if (last == string.Empty)
            { lbErrorMsg.Text = "ERROR!: Last Name is Blank."; lbEr1.Text = "*"; return false; }
            else if (!Val.IsAlpha(last))
            { lbErrorMsg.Text = "ERROR!: Last Name Only letters please."; lbEr1.Text = "*"; return false; }
            // Email
            if (email == string.Empty)
            { lbErrorMsg.Text = "ERROR!: Email is Blank."; lbEr2.Text = "*"; return false; }
            else if (!email.Contains('.') || !email.Contains('@') || !Val.IsAlphaNumSymbols(email, Val._emailSym))
            { lbErrorMsg.Text = "ERROR!: Invalid Email."; lbEr2.Text = "*"; return false; }
            else if (email != emailConfirm)     // Email Equal
            { lbErrorMsg.Text = "ERROR!: Your Email & Email_Confirm is Not Equal."; lbEr2.Text = lbEr3.Text = "*"; return false; }
            // Password
            if (password.Length < 5)
            { lbErrorMsg.Text = "Password Needs to be at least 5 characters & Cannot be empty."; lbEr4.Text = "*"; return false; }
            else if (!Val.IsAlphaNumSymbols(password, Val._passSym, length: 30))
            { lbErrorMsg.Text = "Invalid Password: make sure you are using accepted symbols. Passwords are Case Sensitive."; lbEr4.Text = "*"; return false; }
            else if (password != passConfirm)
            { lbErrorMsg.Text = "Password & Confirm_Password are Not equal."; lbEr4.Text = lbEr5.Text = "*"; return false; }
            // Pass Validation
            return true;
        }

        void ClearError()
        {
            lbErrorMsg.Text = lbSuccess.Text = "";
            lbEr0.Text = lbEr1.Text = lbEr2.Text = lbEr3.Text =
                lbEr4.Text = lbEr5.Text = "";
        }

        protected void btnSubmit_Click(object sender, EventArgs e)
        {
            string first, last, email, password;
            first = last = email = password = "";
            try
            {
                first = txFirst.Text.Trim(); last = txLast.Text.Trim();
                email = txEmail.Text.Trim().ToLower(); password = txPassword.Text.Trim();
                txPassword.Text = "";
                // Validation
                if (!Valid(first, last, email, txEmailConfirm.Text.Trim().ToLower(),
                    password, txPasswordConfirm.Text.Trim())) return;
                // ReCaptcha Val Exit, lbErrorMsg in Function
                double humanScore;
                if (!CheckRecaptcha(email, out humanScore))
                    return;
                // SQL Check if user already exists
                if (_sql.State == ConnectionState.Closed) _sql.Open();
                var com = new SqlCommand("Select count(*) FROM [UserData] WHERE Email=@Email", _sql);
                com.Parameters.AddWithValue("@Email", email);
                if ((int)com.ExecuteScalar() > 0)
                { lbErrorMsg.Text = "There is already a User Registered with that Email.<br />Your Username is your Email. Please Select LOGIN > ‘Forgot Password’ Link to Recover a forgotten Password."; return; }
                // SQL Add User to Database
                com = new SqlCommand("INSERT INTO [UserData] (First, Last, Email, Password) VALUES (@First, @Last, @Email, @Password)", _sql);
                com.Parameters.AddWithValue("@First", Val.UpperFirst(first));
                com.Parameters.AddWithValue("@Last", Val.UpperFirst(last));
                com.Parameters.AddWithValue("@Email", email);
                com.Parameters.AddWithValue("@Password", Val.Encrypt(password));
                if (com.ExecuteNonQuery() != 1) throw new Exception("ERROR!: User was Not registered, user: " + email);
                lbSuccess.Text = "Successfully Registered!";
                // SQL Get Primary Key: to save session 'Info' Class
                com = new SqlCommand("SELECT [id],[First] FROM [UserData] WHERE [Email]=@Email", _sql);
                com.Parameters.AddWithValue("@Email", email);
                var reader = com.ExecuteReader();
                if (!reader.Read()) throw new Exception("Could Not find prev. inserted row. email: " + email);
                Info info;
                Session["data"] = info = new Info(reader["id"], reader["First"], email);
                reader.Close();
                // SQL ReCaptcha Add HumanScore
                com = new SqlCommand("INSERT INTO [HumanScore] ([userid],[humanScore],[dateTime],[page]) VALUES (@userid,@humanScore,@dateTime,@page)", _sql);
                com.Parameters.AddWithValue("@userid", info.id);
                com.Parameters.AddWithValue("@humanScore", humanScore);
                com.Parameters.AddWithValue("@DateTime", Data.DateTimeValue(DateTime.Now));
                com.Parameters.AddWithValue("@page", "Register");
                if (com.ExecuteNonQuery() == 0) throw new Exception("Could Not save ReCaptcha data email: " + email);
            }
            catch (Exception ex) // Exit
            { _sql.Close(); lbErrorMsg.Text = Logger._erMsg; Logger.Log(ex); return; }
            _sql.Close();
            // Browser Back Navigation causes issue: This is why reload.
            Response.Redirect("~/Register.aspx?msg=" + Server.UrlEncode(lbSuccess.Text));
        }

        bool CheckRecaptcha(string email, out double score)
        {
            Recaptcha.Token token;
            score = 0;
            // Val ReCaptchaV2 Checkbox
            if (!Recaptcha.IsValidV3(Request.Form["g-recaptcha-response"], false, out token))
            {
                lbErrorMsg.Text = "Please check the ReCaptcha checkbox at the bottom and follow the prompt if need be. Additionally, check the checkbox before clicking the submit button.";
                return false;
            }
            // Val ReCaptchaV3
            if (!Recaptcha.IsValidV3(Request.Form["g-recaptcha-responsev3"], true, out token))
                throw new Exception("ReCaptchaV3 was unsuccessful. email: " + email);
            score = token.Score;
            return token.Success;
        }

        // Gets the Keys by Host LocalHost or Supracharger.com
        protected Recaptcha.Version RecaptchaVersion(bool isV3) { return Recaptcha.GetVersionHost(HttpContext.Current.Request, isV3); }
    }
}