using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.Data;
using System.Data.SqlClient;
using Stripe;
using Newtonsoft.Json;

namespace SupraChargerWeb
{
    public partial class Cart : System.Web.UI.Page
    {
        SqlConnection _sql;
        List<CartItem> _cart;

        public Cart() { _sql = new SqlConnection(Data._sqlConnection); }

        protected void Page_Load(object sender, EventArgs e)
        {
            Clear();
            _cart = Session["Cart"] as List<CartItem>;
            // Redirect: No Items in Cart
            if (_cart == null)
                Response.Redirect("~/CartNone.aspx");
            // If Remove Item
            if (Request.QueryString["remove"] != null)
                RemoveItem(Request.QueryString["remove"]);
            if (!IsPostBack)
            {
                // Grab SQL Data & Create DataTable
                var data = GetAllItems(string.Join(" ,", _cart.Select(ii => "'" + ii._model + "'")));
                double subTotal;
                DataTable table = CreateTable(data, _cart, out subTotal);
                gvCart.DataSource = table;
                gvCart.DataBind();
                // Total Price
                lbTotal.Text = lbSubTotal.Text = "$" + Math.Round(subTotal, 2);
                // Payment Re-Post
                if (!string.IsNullOrEmpty(Request.Form["stripeToken"]) && Session["Data"] != null)
                    ExecuteOrder(data);
            }
        }

        void UpdateCart(out double total)
        {
            // Get Sub-Total
            total = GetTotal(_cart);     
            // Display to User
            lbTotal.Text = lbSubTotal.Text = "$" + total;   // Total Already Rounded
        }

        // MAIN
        void ExecuteOrder(Dictionary<string, Dictionary<string, object>> data)
        {
            Info info = (Info)Session["Data"];
            string desc = string.Join(", ", _cart.Select(v => v.ToString()));
            int orderId;
            if (_sql.State == ConnectionState.Closed) _sql.Open();
            try
            {
                if (info.Email == null) throw new Exception();

                // Re-Captcha: Exit
                double score;
                if (!CheckRecaptcha(info.Email, out score)) return;
                if (score < 0.40)
                { lbError.Text = "Whoops! It looks like your being categorized like a robot."; return; }
                // Get Order Id ------------------------------
                orderId = GetOrderId(info, desc);
                // Clone Cart to Inhibit Tampering -----------
                List<CartItem> cart = _cart.Select(v => v.Clone()).ToList();
                // Payment: Stripe----------------------------
                if (!MakePayment(Request.Form["stripeToken"], orderId, desc, cart))
                    return;
                // SQL Update values to Current & Valid=True, since the values my be wrong
                // userid is set in GetOrderId()
                var com = new SqlCommand("UPDATE [Orders] SET [user]=@user,[description]=@desc,[price]=@price," +
                                "[dateTime]=@dateTime,[valid]=1 WHERE [id]=@id", _sql);
                com.Parameters.AddWithValue("@user", info.Email);
                com.Parameters.AddWithValue("@id", orderId);
                com.Parameters.AddWithValue("@desc", desc);
                com.Parameters.AddWithValue("@price", GetTotal(cart));
                com.Parameters.AddWithValue("@dateTime", DateTime.Now.ToString());
                if (com.ExecuteNonQuery() == 0) throw new Exception(info.Email);

                // Create Licenses -------
                if (!GrabLicense(cart, info, orderId, data)) return;
                // Clear Cart
                Session["Cart"] = null;
            }
            catch (Exception e)
            { _sql.Close(); lbError.Text = Logger._erMsg; Logger.Log(e); return; }
            _sql.Close();

            // Redirect If it went well, [[*n*]]: newlineChar
            string msg = "Thank you for your order! An email has been sent with the included purchased license(s). You should " +
                "see the email within a few minutes, if not 24 hours. [[*n*]][[*n*]]Your Order number is: #" + orderId +" Thank you for your purchase!";
            Response.Redirect("~/CartNone.aspx?msg=" + Server.UrlEncode(msg));
        }

        // Gets OrderID for ExecuteOrder(), All this helps to not have multiple empty orders
        int GetOrderId(Info info, string desc)
        {
            int orderId;
            // SQL See if existing Order
            var com = new SqlCommand("SELECT count([id]) FROM [Orders] WHERE [userid]=@userid  AND [valid] IS NULL", _sql);
            com.Parameters.AddWithValue("@userid", info.id);
            // SQL if order was Not found & need to create new
            // Values are Temporary & will be updated after payment is verified
            if ((int)com.ExecuteScalar() == 0)
            {
                // Insert with current could be wrong values
                com = new SqlCommand("INSERT INTO [Orders] ([user],[userid],[description],[price]) VALUES" +
                                        "(@user,@userid,@desc,@price)", _sql);
                com.Parameters.AddWithValue("@user", info.Email);
                com.Parameters.AddWithValue("@userid", info.id);
                com.Parameters.AddWithValue("@desc", desc);
                com.Parameters.AddWithValue("@price", GetTotal(_cart));
                if (com.ExecuteNonQuery() == 0) throw new Exception(info.Email);
            }

            // SQL Read Order
            com = new SqlCommand("SELECT [id] FROM [Orders] WHERE [userid]=@userid AND [valid] IS NULL", _sql);
            com.Parameters.AddWithValue("@userid", info.id);
            var reader = com.ExecuteReader();
            if (!reader.Read()) throw new Exception(info.Email);
            orderId = (int)(long)reader["id"];
            reader.Close();

            return orderId;
        }
        // Uses Stripe to Make Payment
        bool MakePayment(string token, int orderId, string desc, List<CartItem> cart)
        {
            // Set your secret key: remember to change this to your live secret key in production
            // See your keys here: https://dashboard.stripe.com/account/apikeys
            StripeConfiguration.SetApiKey(Data._stripeSercret);

            //// Token is created using Checkout or Elements!
            //// Get the payment token submitted by the form:
            //var token = model.Token; // Using ASP.NET MVC

            var options = new ChargeCreateOptions
            {
                Amount = (int)(GetTotal(cart) * 100),
                Currency = "usd",
                Description = desc,
                SourceId = token,
                Metadata = new Dictionary<string, string>() { { "OrderId", orderId.ToString() } }
            };
            // Charge Card
            var service = new ChargeService();
            Charge charge;
            try { charge = service.Create(options); }
            catch (StripeException ex)
            {
                lbError.Text = "There was an error with your payment. If your payment information seems correct, contact your " +
                                "cardholder directly by phone to solve the issue. Reason: " + ErrorStripe(ex);
                return false;     // Exit
            }
            if (charge.Status != "succeeded")
            { lbError.Text = "There was an error with your payment information"; return false; }
            // Return True
            return true;
        }
        // Re-Captcha
        bool CheckRecaptcha(string email, out double score)
        {
            Recaptcha.Token token;
            score = 0;
            // Val ReCaptchaV3
            if (!Recaptcha.IsValidV3(Request.Form["g-recaptcha-responsev3"], true, out token))
                throw new Exception("ReCaptchaV3 was unsuccessful. email: " + email);
            score = token.Score;
            return token.Success;
        }
        // Gets License, updates SQL, sends Email
        bool GrabLicense(List<CartItem> cart, Info info, int orderId, Dictionary<string, Dictionary<string, object>> data)
        {
            // Get num of Licenses
            int numL = cart.Select(v => v._num).Sum();
            // Create Licenses
            GetLicense GL = new GetLicense(_sql);
            var licenses = new int[numL].Select(v => GL.GetIt()).ToList();
            if (_sql.State == ConnectionState.Closed) _sql.Open();      // Re-Check
            // SQL Submit each License
            int ix = 0;
            foreach(var item in cart)
                for (int i = 0; i < item._num; i++)
                {
                    var com = new SqlCommand("INSERT INTO [License] ([License],[userid],[user],[orderId],[model])" +
                                "VALUES (@License,@userid,@user,@orderId,@model)", _sql);
                    com.Parameters.AddWithValue("@License", licenses[ix++].ToUpper());
                    com.Parameters.AddWithValue("@userid", info.id);
                    com.Parameters.AddWithValue("@user", info.Email);
                    com.Parameters.AddWithValue("@orderId", orderId);
                    com.Parameters.AddWithValue("@model", item._model);
                    if (com.ExecuteNonQuery() == 0) throw new Exception(info.Email);
                }

            // Send Email -------------
            string header = $"Thank You for Your Order!";
            string msg = $"<p>Order#: {orderId}<br />Total: ${GetTotal(cart)}<br />Date: {DateTime.Now}</p>\n";
            msg += $"<p>Thank you for your order {info.First}!</p>\n" +
                        "<p>Here are your purchased License(s):</p>\n";
            ix = 0;
            // Loop Each Item to get each License
            msg += "<p>";
            foreach(var item in cart)
            {
                msg += data[item._model]["description"] + ":<br />";
                for (int i = 0; i < item._num; i++)
                    msg += "&emsp;&emsp;" + licenses[ix++].ToUpper() + "<br />";
            }
            msg += "</p>\n";
            msg += "<p><b>NOTE!:</b> Please be sure to keep these Licenses in a safe place.</p>";
            msg += "<p><b>To Activate Product:</b>If you have not already done so, please go to the page of the product you purchased and download the software. Then, copy and paste " +
                "the current License above into the &quot;License&quot; field to Activate the software.</p>";
            // HTML Template
            string html = System.IO.File.ReadAllText(Data._otherFiles + "/Email.html");
            html = html.Replace("[[*header*]]", header).Replace("[[*body*]]", msg);
            // Send Email: Func Starts new Task to send Email
            Data.SendEmail("Your Order at SupraCharger.com", html, info.Email, isBodyHtml:true);
            // Return
            return true;
        }
        

        Dictionary<string, Dictionary<string, object>> GetAllItems(string models)
        {
            // SQL
            if (_sql.State == ConnectionState.Closed) _sql.Open();
            var com = new SqlCommand($"SELECT [model],[price],[description],[picture] FROM [Items] WHERE [model] IN ({models})", _sql);
            var reader = com.ExecuteReader();
            var D = new Dictionary<string, Dictionary<string, object>>();
            while (reader.Read())
            {
                var row = Enumerable.Range(0, reader.FieldCount).ToDictionary(reader.GetName, reader.GetValue);
                D.Add(row["model"].ToString(), row);
            }
            reader.Close();
            _sql.Close();
            return D;
        }

        DataTable CreateTable(Dictionary<string, Dictionary<string, object>> D, List<CartItem> cart, out double total)
        {
            total = 0;
            // Header
            DataTable dt = new DataTable();
            dt.Columns.Add("Description", typeof(string));
            dt.Columns.Add("Price", typeof(string));
            dt.Columns.Add("Qty", typeof(string));
            dt.Columns.Add("Picture", typeof(string));
            // Add by row
            foreach (var item in cart)
            {
                var row = dt.NewRow();
                var values = D[item._model];
                double price = (double)float.Parse(values["price"].ToString());
                row["Qty"] = item._num.ToString();
                row["Description"] = values["description"].ToString();
                row["Price"] = "$" + Math.Round(price, 2);
                row["Picture"] = values["picture"];
                item.SetPrice(price);
                dt.Rows.Add(row);
                // Sum Total
                total += price * item._num;
            }
            return dt;
        }

        // Occurs on each Row when Data is Bounded
        protected void gvCart_RowDataBound(object sender, GridViewRowEventArgs e)
        {
            // Remove Boader
            foreach (TableCell tc in e.Row.Cells)
                tc.BorderStyle = BorderStyle.None;
            // Exit if Not dataRow
            if (e.Row.RowType != DataControlRowType.DataRow) return;
            int rIdx = e.Row.DataItemIndex;          // Row Index
            DataRow data = ((DataRowView)e.Row.DataItem).Row;
            // Set Description
            Label desc = (Label)e.Row.FindControl("lbDescription");
            desc.Text = data["description"].ToString();
            // Set Remove HyperLink
            HyperLink link = (HyperLink)e.Row.FindControl("lkRemove");
            link.NavigateUrl = HttpContext.Current.Request.Url.AbsoluteUri + $"?remove={rIdx}";
            // Image
            Image img = (Image)e.Row.FindControl("img");
            img.ImageUrl = data["Picture"].ToString();
            // Fill Qty DropDownList
            DropDownList dd = (DropDownList)e.Row.FindControl("ddlQtyList");
            //var dd = e.Row.Cells[2].Controls[1] as DropDownList;
            for (int i = 1; i <= 10; i++)
                dd.Items.Add(i.ToString());
            // Select Num. from List
            dd.Items.FindByValue(data["Qty"].ToString()).Selected = true;
        }
        // Qty ListBox Selected Click
        protected void QtyList_SelectedIndexChanged(object sender, EventArgs e)
        {
            DropDownList ddList = (DropDownList)sender;
            GridViewRow grdrDropDownRow = ((GridViewRow)ddList.Parent.Parent);
            //DropDownList ddList = (DropDownList)grdrDropDownRow.FindControl("ddlQtyList");
            // Update Cart Num.
            int num;
            if (!int.TryParse(ddList.SelectedValue, out num)) num = 1;
            _cart[grdrDropDownRow.RowIndex].SetNum(num);
            // Update Cart
            double subTotal;
            UpdateCart(out subTotal);
        }
        // Remove Item from Cart
        void RemoveItem(string indexStr)
        {
            // Get & Exit on Invalid Index
            int index;
            if (!int.TryParse(indexStr, out index) || index < 0 || index >= _cart.Count) return;
            // Remove in GridView & List Cart
            gvCart.DeleteRow(index);
            Session["Cart"] = _cart = CartItem.RemoveItem(_cart, _cart[index]._model);  // Use Func. so Count==0 is null
            // Reload Cart by reloading Page
            string url = Data.UrlClean(HttpContext.Current.Request.Url.AbsoluteUri);
            Response.Redirect(url);
        }

        // Returns user Error from Stripe Payment Exception
        string ErrorStripe(StripeException e)
        {
            switch (e.StripeError.ErrorType)
            {
                case "card_error":
                case "api_connection_error":
                    return e.StripeError.Message;
                    //Console.WriteLine("Code: " + e.StripeError.Code);
                    //Console.WriteLine("Message: " + e.StripeError.Message);
                    //break;
                //case "api_error":
                //    break;
                //case "authentication_error":
                //    break;
                //case "invalid_request_error":
                //    break;
                //case "rate_limit_error":
                //    break;
                //case "validation_error":
                //    break;
                default:
                    return null;
            }
        }
        // Returns Total: Rounds Sum
        public double GetTotal(List<CartItem> crt) { return Math.Round(crt.Select(ii => ii._price * ii._num).Sum(), 2); }
        // Clear Errors
        void Clear() { lbError.Text = ""; }
        // Just a blank method NEEDED for GridView.DeleteRow()
        protected void gvCart_RowDeleting(object sender, GridViewDeleteEventArgs e) { }
        // Gets the Keys by Host LocalHost or Supracharger.com
        protected Recaptcha.Version RecaptchaVersion(bool isV3) { return Recaptcha.GetVersionHost(HttpContext.Current.Request, isV3); }

        // If Not LoggedIn: LogIn, or Payment button
        protected string PaymentLogin()
        {
            // If Needs to Login
            if (Session["Data"] == null)
            {
                string url = ((Master)Master).LoginPostAddr("Login.aspx") + "&msg=" +
                                Server.UrlEncode("Please Register or Login to complete CHECKOUT. [[*n*]][[*n*]]Registration is Quick and Easy!");
                return "<p><img src=\"imgAQ/icons/StripePayment.png\" alt=\"Stripe Checkout\" width=\"185\" height=\"185\" />" +
                    "<a href=\"" + url + "\" class=\"mybtn-arrow\"><b><span>CHECKOUT</span></b></a></p>";
            }

            // If doesn't need to Login: Run Checkout

            return "<form action=\"" + HttpContext.Current.Request.Url.AbsoluteUri + "\" method=\"POST\">\n" +
                  "<div>\n" +
                  "<img src=\"imgAQ/icons/StripePayment.png\" alt=\"Stripe Checkout\" width=\"185\" height=\"185\" />\n" +
                  "<script\n" +
                  "  src=\"https://checkout.stripe.com/checkout.js\" class=\"stripe-button\"\n" +
                  "  data-key=\"" + Data._stripePublish + "\"\n" +
                  "  data-amount=\"" + (int)(GetTotal(_cart) * 100) + "\"\n" +
                  "  data-name=\"SupraCharger.com\"\n" +
                  "  data-description=\"Checkout\"\n" +
                  "  data-image=\"https://stripe.com/img/documentation/checkout/marketplace.png\"\n" +
                  "  data-locale=\"auto\"\n" +
                  "  data-zip-code=\"true\">\n" +
                  "</script>\n" +
                  "<input type=\"hidden\" id=\"g-recaptcha-responsev3\" name=\"g-recaptcha-responsev3\" />\n" +
                    "<script>\n" +
                    "    grecaptcha.ready(function() {\n" +
                    "        grecaptcha.execute('" + RecaptchaVersion(true)._siteKey + "', {action: 'register'})\n" +
                    "            .then(function(token) {\n" +
                    "        // Verify the token on the server.\n" +
                    "        document.getElementById('g-recaptcha-responsev3').value = token;\n" +
                    "        });\n" +
                    "    });\n" +
                    "</script>\n" +
                  "</div>\n" +
                "</form>" +
                "<div><h2>Please press &quot;Pay with Card&quot; to process your card and complete Checkout.</h2></div>";
        }
    }
}