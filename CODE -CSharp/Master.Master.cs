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
    public partial class Master : System.Web.UI.MasterPage
    {
        public string _dir = "";

        protected void Page_Load(object sender, EventArgs e)
        {

        }

        protected string CartImage()
        {
            if (Session["Cart"] == null)
                return _dir + "imgAQ/Cart/Cart-off.png";
            return _dir + "imgAQ/Cart/Cart-on.png";
        }
        
        // Login/ Logout Message
        protected string loginMessage()
        {
            string loginMsg;
            // Welcome & Log out message
            if (Session["Data"] != null)
            {
                string welcome = "";
                try { welcome = $"Hey {((Info)Session["Data"]).First}!"; }
                // Reload Page
                catch { Session["Data"] = null; Response.Redirect(Request.RawUrl); }
                loginMsg =
                    $"<a href=\"{_dir}MyAccount.aspx\"><img src=\"{_dir}imgAQ/gear-small.png\" alt=\"Settings\"></a>\n<p>" +
                  welcome + $"&emsp;&emsp;&emsp;<a href= \"{_dir}Logout.aspx\" role=\"button\">Log Out</a></p>";
            }
            // Login Message
            else
                loginMsg =
                    $"<a href=\"#myModal\"><img src=\"{_dir}img/icons/icon-user.png\" alt=\"icon\"></a>\n" +
                  $"<p><a href= \"#myModal\" role=\"button\" data-toggle=\"modal\">Login</a>&emsp;&emsp;&emsp;<a href= \"{_dir}Register.aspx\" role=\"button\">Register</a></p>";
            return loginMsg;
        }

        // Used with Form to send Form to a Different Page
        public string LoginPostAddr(string dest)
        {
            string url = Data.UrlClean("~" + HttpContext.Current.Request.Url.AbsolutePath);
            // Return
            // need _dir to goto login page, absolute path is the url after login
            return _dir + dest + "?url=" + Server.UrlEncode(url);
        }

        public string HeaderRoulette() { return ""; }
        public string HeaderMining() { return ""; }
        public string MiddleRoulette() { return ""; }
        public string MiddleMining() { return ""; }
        // Footer: Left Side Bar can be included in footer
        public string FooterRoulette()
        {
            return "<div class=\"sidenav\">\n" +
              $"<a href=\"{_dir}Products.aspx\" target=\"_blank\"><img src=\"{_dir}RouletteAnalyzer/img/main/mini-main.png\" alt=\"Our Software Roulette Analyzer\" /></a>\n" +
              $"<p><a href=\"{_dir}Products.aspx\" class=\"mybtn\" target=\"_blank\">Free Trial</a></p>\n" +
              "</div>\n";
        }
        public string FooterMining()
        {
            return "<div class=\"sidenav\">\n" +
              $"<a href=\"{_dir}Products.aspx\" target=\"_blank\"><img src=\"{_dir}MiningControl/img/main/Mining_Control_Mini.png\" alt=\"Our Software Mining Control Pro\" /></a>\n" +
              $"<p><a href=\"{_dir}Products.aspx\" class=\"mybtn\" target=\"_blank\">Free Trial</a></p>\n" +
              "</div>\n";
        }

        protected string UrlFooter()
        {
            return "<ul>\n" +
                "<li><a href=\"" + _dir + "index.aspx\">Home</a></li>\n" +
                "<li><a href=\"" + _dir + "Products.aspx\">Products</a>\n" +
                  "<ul>\n" +
                    "<li><a href=\"" + _dir + "MiningControl/MiningControl.aspx\">&emsp;Mining Control Pro</a> </li>\n" +
                    "<li><a href=\"" + _dir + "RouletteAnalyzer/RouletteAnalyzer.aspx\">&emsp;Roulette Analyzer</a> </li>\n" +
                  "</ul>\n" +
                "</li>\n" +
                "<li><a href=\"" + _dir + "Help.aspx\">Help</a>\n" +
                "</li>\n" +
                "<li><a href=\"" + _dir + "Support.aspx\">Contact</a></li>\n" +
            "</ul>";
        }

        protected string Nav()
        {
            return "<div id=\"smoothmenu\" class=\"ddsmoothmenu\">\n" +
              "  <ul id=\"nav\">\n" +
				"  <li><a href=\"" + _dir + "index.aspx\">Home</a></li>\n" +
                "<li><a href=\"" + _dir + "Products.aspx\">Products</a>\n" +
                "  <ul>\n" +
                "    <li><a href=\"" + _dir + "Products.aspx\">Products</a></li>\n" +
                "    <li><a href=\"" + _dir + "MiningControl/MiningControl.aspx\">Mining Control Pro</a></li>\n" +
                "    <li><a href=\"" + _dir + "RouletteAnalyzer/RouletteAnalyzer.aspx\">Roulette Analyzer</a></li>\n" +
                "  </ul>\n" +
                "</li>\n" +
                "<li><a href=\"" + _dir + "Help.aspx\">Help</a>\n" +
                "  <ul>\n" +
                "    <li><a href=\"" + _dir + "Help.aspx\">Help</a></li>\n" +
                "    <li><a href=\"" + _dir + "MiningControl/help/GettingStarted.aspx\">Help: Mining Control Pro</a></li>\n" +
                "    <li><a href=\"" + _dir + "RouletteAnalyzer/help/GettingStarted.aspx\">Help: Roulette Analyzer</a></li>\n" +
                "  </ul>\n" +
                "</li>\n" +
                "<li><a href=\"" + _dir + "Support.aspx\">Contact</a></li>\n" +
                "<li><a href=\"" + _dir + "Products/Blog.aspx\">Blog</a>\n" +
                "  <ul>\n" +
                "    <li><a href=\"" + _dir + "Products/Blog.aspx\">Blog</a></li>\n" +
                "    <li><a href=\"" + _dir + "MiningControl/blog/mainMining.aspx\">Blog: Crypto Mining</a></li>\n" +
                "    <li><a href=\"" + _dir + "RouletteAnalyzer/blog/mainCasino.aspx\">Blog: Casino</a></li>\n" +
                "  </ul></li>\n" +
              "</ul>\n" +
            "</div>\n";
        }

        protected string JQueries()
        {
            return $"<script src=\"{_dir}js/bootstrap.js\" type=\"text/javascript\"></script>\n" +
            $"<script src=\"{_dir}js/ddsmoothmenu.js\" type=\"text/javascript\"></script>\n" +
            $"<script src=\"{_dir}js/jquery.validate.js\" type=\"text/javascript\"></script>\n" +
            $"<script src=\"{_dir}js/jquery.form.js\" type=\"text/javascript\"></script>\n" +
            $"<script src=\"{_dir}twitter/twitter.js\" type=\"text/javascript\"></script>\n" +
            $"<script src=\"{_dir}js/jquery.easing.1.3.js\" type=\"text/javascript\"></script>\n" +
            $"<script src=\"{_dir}js/selectnav.js\" type=\"text/javascript\"></script>\n" +
            $"<script src=\"{_dir}js/custom.js\" type=\"text/javascript\"></script>\n";
        }
    }
}