using System.Data.Common;
using System.Linq;
using System;
using System.Collections.Generic;
using System.Net;
using System.Web;
using src.Core.Domains;
using Microsoft.Identity.Client;

namespace src.Web.Extensions
{
    public static class MessageExtension
    {

        public static string ReplaceForValidation(Parent model, string template)
        {

            var tokens = new Dictionary<string, string>()
                {
                    {"[active_code]", WebUtility.HtmlEncode(model.VerifyCode)},
            };
            foreach (string token in tokens.Keys)
                template = template.Replace(token, tokens[token]);

            return template;
        }
        public static string ReplaceForSmsValidation(Parent model, string template)
        {

            var tokens = new Dictionary<string, string>()
            {
                {"[active_code]", WebUtility.HtmlEncode(model.VerifyCode)},
            };
            foreach (string token in tokens.Keys)
                template = template.Replace(token, tokens[token]);

            return template;
        }

        public static string ReplaceForUserConfirmation(User data,ChangeRequest changeRequest, string template, string host)
        {
            string status = changeRequest.ChangeRequestFlow.Status == false && changeRequest.ChangeRequestFlow.State != "Completed"
                            ? "Pending"
                            : changeRequest.ChangeRequestFlow.Status == true
                                ? "Refused"
                                : "Completed";
            var currentDate = DateTime.Now;
            var tokens = new Dictionary<string, string>()
            {
                { "[User_LastName]", WebUtility.HtmlEncode(changeRequest.Requester)},
                { "[User_FirstName]", WebUtility.HtmlEncode("")},
                { "[User_UserName]", WebUtility.HtmlEncode(changeRequest.Requester)},

                { "[ChangeRequest_Id]", WebUtility.HtmlEncode(changeRequest.Id.ToString())},
                { "[ChangeRequest_CreatedDate]", WebUtility.HtmlEncode(currentDate.ToString("dd/MM/yyyy")) },
                { "[ChangeRequestFlow_State]", WebUtility.HtmlEncode(changeRequest.ChangeRequestFlow.State)},
                { "[ChangeRequestFlow_User_UserName]", WebUtility.HtmlEncode(changeRequest.ChangeRequestFlow.User.UserName)},           
                { "[ChangeRequestFlow_Status]", WebUtility.HtmlEncode(status)},
              

            };
            foreach (string token in tokens.Keys)
                template = template.Replace(token, tokens[token]);

            return template;
        }

        public static string ReplaceForUserInCharger(User user, User userInCharge,Guid id, string template, string host)
        {
           
            var currentTime = DateTime.Now;
            var tokens = new Dictionary<string, string>()
            {
                { "[User_LastName]", WebUtility.HtmlEncode(userInCharge.LastName)},
                { "[User_FirstName]", WebUtility.HtmlEncode(userInCharge.FirstName)},
                { "[User_UserName]", WebUtility.HtmlEncode(user.UserName)},

                { "[CreatedDate]", WebUtility.HtmlEncode(currentTime.ToString("dd/MM/yyyy"))},

                { "[ChangeRequest_Id]", WebUtility.HtmlEncode(id.ToString())},
                { "[ChangeRequest_Link]", WebUtility.HtmlEncode(host + "/Administration/Requests/Detail/" + id)}

            };
            foreach (string token in tokens.Keys)
                template = template.Replace(token, tokens[token]);

            return template;
        }

    }
}