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
    public partial class Newsletter : System.Web.UI.Page
    {
        SqlConnection _sql;

        public Newsletter() { _sql = new SqlConnection(Data._sqlConnection); }

        protected void Page_Load(object sender, EventArgs e)
        {
            txEmail.ReadOnly = true;        // Don't want user to change email from there user email
            // Have user Login if wanting to Subscribe to the Newsletter
            if (Session["Data"] == null)
            {
                string msg = "NOTE!: Please Login or Register to Subscribe to our Newsletter. \rRegistration is Quick & Easy!";
                Response.Redirect("~/Login.aspx?msg=" + Server.UrlEncode(msg));
            }
            else
            { try { txEmail.Text = ((Info)Session["Data"]).Email; } catch { } }
            // User change email
            lbSuccess.Text = "If you would like to change your email address, please goto Settings -> Edit User Info.";

            if (!IsPostBack)
            {
                // Successfully Subscibed with Form
                if (Request.QueryString["success"] != null)
                {
                    lbSuccess.Text = "Successfully Subscribed!";
                    btnSubmit.Enabled = false;
                    return;
                }
                // Previous Page Post
                if (Request.Form.HasKeys() && string.IsNullOrEmpty(txEmail.Text))
                {
                    string email = Request.Form["email"];
                    // If has value for email, Run button click funtion
                    if (email != null)
                    {
                        txEmail.Text = email;
                        btnSubmit_Click(null, null);
                    }
                }
            }
        }

        void Clear() { lbErrorMsg.Text = lbSuccess.Text = ""; }

        // Validates but takes No Action if Clear
        protected void btnSubmit_Click(object sender, EventArgs e)
        {
            try
            {
                Clear();
                string email = txEmail.Text.Trim();
                // Val
                if (email.Length == 0)
                { lbErrorMsg.Text = "Email can Not be Blank."; return; }
                if (!Val.IsAlphaNumSymbols(email, Val._emailSym) ||
                    !email.Contains('@') || !email.Contains('.'))
                { lbErrorMsg.Text = "Invalid Email Symbols."; return; }

                // Update Subscribe in SQL
                Info info = (Info)Session["Data"];
                if (_sql.State == ConnectionState.Closed) _sql.Open();
                var com = new SqlCommand("UPDATE [UserData] SET [Subscribe]=1 WHERE [id]=@id", _sql);
                com.Parameters.AddWithValue("@id", info.id);
                if (com.ExecuteNonQuery() != 1) throw new Exception("Could Not update row ");
            }
            catch (Exception ex)
            { _sql.Close();  lbErrorMsg.Text = Logger._erMsg; Logger.Log(ex); return; }
            _sql.Close();

            // Redirect
            Response.Redirect(Data.UrlClean(HttpContext.Current.Request.Url.AbsoluteUri) + "?success=True");
            //string url = Request.QueryString["url"];
            //if (!string.IsNullOrEmpty(url))
            //    Response.Redirect(url);
            //else Response.Redirect("~/index.aspx");
        }
    }
}