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
    public partial class PassResetVal : System.Web.UI.Page
    {
        SqlConnection _sql;

        public PassResetVal()
        {
            _sql = new SqlConnection(Data._sqlConnection);
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            Clear();
            // Validate EMPTY code QueryString
            if (string.IsNullOrEmpty(Request.QueryString["code"]))
            { lbErrorMsg.Text = "Invalid Authorization Code. Please try a different link or Contact Support."; Lock(); return; }
            else if (!btnReset.Enabled) Lock(false);
        }

        protected void btnReset_Click(object sender, EventArgs e)
        {
            string code = Request.QueryString["code"].Trim();
            string pass = txPass.Text.Trim(), confirm = txConfirm.Text.Trim();
            // VAL Password
            if (pass.Length < 5)
            { lbErrorMsg.Text = "Password Needs to be at least 5 characters & Cannot be empty."; return; }
            else if (!Val.IsAlphaNumSymbols(pass, Val._passSym, length: 30))
            { lbErrorMsg.Text = "Invalid Password: make sure you are using accepted symbols. Passwords are Case Sensitive."; return; }
            else if (pass != confirm)
            { lbErrorMsg.Text = "Password & Confirm_Password are Not equal."; return; }
            // VAL code
            if (string.IsNullOrEmpty(code) || !Val.IsAlphaNum(code))
            { lbErrorMsg.Text = "Invalid Authorization Code. Please try a different link or Contact Support."; Lock(); return; }

            try
            {
                // SQL Find by code
                if (_sql.State == ConnectionState.Closed) _sql.Open();
                var com = new SqlCommand("SELECT [expiry],[user] FROM [PasswordReset] WHERE [code]=@code", _sql);
                com.Parameters.AddWithValue("@code", code);
                var reader = com.ExecuteReader();
                if (!reader.Read())
                {
                    lbErrorMsg.Text = "Authorization Code may have Expired. <br />Please go through the process again by going to " + 
                        "Login & selecting 'Forgot Password Link' or Contact Support.";
                    Lock(); return;
                }
                string email = reader["user"].ToString();
                var expiry = DateTime.Parse(reader["expiry"].ToString());
                if (reader.Read()) throw new Exception("Extra rows. authCode: " + code);
                reader.Close();
                // SQL Delete Auth. Code
                com = new SqlCommand("DELETE FROM [PasswordReset] WHERE [code]=@code", _sql);
                com.Parameters.AddWithValue("@code", code);
                if (com.ExecuteNonQuery() == 0) throw new Exception("Could Not delete authCode row. code: " + code);
                // Check Expiry
                if (expiry.Subtract(DateTime.Now).TotalMinutes < 0)
                {
                    lbErrorMsg.Text = "Sorry, your Password Authorization code has Expired. <br />Please go through the process again by going to " +
                          "Login & selecting 'Forgot Password Link' or Contact Support.";
                    Lock();
                    return;
                }
                // SQL Update Password
                // WHERE Email: has to be email cuz that is whats in PasswordReset, and [PasswordRest] is very current & will be deleted after use.
                com = new SqlCommand("UPDATE [UserData] SET [Password]=@Password WHERE [Email]=@Email", _sql);
                com.Parameters.AddWithValue("@Password", Val.Encrypt(pass));    // Encypt Password
                com.Parameters.AddWithValue("@Email", email);
                if (com.ExecuteNonQuery() == 0) throw new Exception("Could Not update sql password. email: " + email);
            }
            catch (Exception ex)    // EXIT: sql Close included
            { _sql.Close(); lbErrorMsg.Text = Logger._erMsg; Logger.Log(ex); return; }
            _sql.Close();

            // Navigate to login with message, otherwise it will redirect them to this page
            string msg = "Password Reset Successful. You have Authorization to Login with your new password.";
            Response.Redirect("~/Login.aspx?msg=" + Server.UrlEncode(msg));
        }

        void Clear() { lbErrorMsg.Text = lbMsg.Text = ""; }
        void Lock(bool lockIt = true) { btnReset.Enabled = txPass.Enabled = txConfirm.Enabled = !lockIt; }
    }
}