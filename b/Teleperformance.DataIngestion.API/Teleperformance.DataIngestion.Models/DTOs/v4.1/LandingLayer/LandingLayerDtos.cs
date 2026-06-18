using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Teleperformance.DataIngestion.Models.DTOs.v4._1.LandingLayer
{
    public class LandingLayerDtos
    {
    }

    public class FileExtensionDto
    {
        public int id { get; set; }
        public string fileExtension { get; set; }
    }

    public class PrefixesDto
    {
        public int id { get; set; }
        public string prefixName { get; set; }
    }

    public class LandingLayerUploadConfigurationDto
    {
        public int noOfAllowedFilesToUpload { get; set; }
        public int totalFileSize { get; set; }

    }

    public class LandingLayerInsertConfigurationRequest
    {
        public string? userName { get; set; }
        public string? loggedInUser { get; set; }
        public string? MyJson { get; set; }
        public string? uploadFileId { get; set; }
        public string? processName { get; set; }
        public string? flpConfigurationId { get; set; }
        public List<IFormFile> Files { get; set; } = new();
    }
}
