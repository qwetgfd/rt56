using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Teleperformance.DataIngestion.Models.DTOs.v4._1.DataAssists
{
    public class UserFormRequest
    {

    }

    public class ValidationRule
    {
        public string rule { get; set; }
        public string function_name { get; set; }
        public string code { get; set; }
    }

    public class ProjectQueryFormRequest
    {
        //public  int flpRulesetId { get; set; }
        //public int chatId { get; set; }
        //public string userInput { get; set; }
        public string? userContext { get; set; }
        //public string userUpn { get; set; }
        //public string chatArea { get; set; }
        public string validationRules { get; set; }
        //public IFormFile File
        //{
        //    get; set;
        //}
        public string fileHeaders { get; set; }
        //public string userSelectedDatabase { get; set; }
        //public string responsetype { get; set; }
        public int flowid { get; set; }
        public int projectId { get; set; }
        public int versionId { get; set; }
        public bool isDataValidation { get; set; }
        public bool isCodeSnippet { get; set; }
        public bool overrideExistingRules { get; set; }
        public  string? csvDataToTest { get; set; }
    }
}
