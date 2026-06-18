using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Teleperformance.DataIngestion.Models.Entities.v1._0
{
    public class APIResponse<T>
    {
        /// <summary>
        /// A code indicating the resulting status of the request.
        /// </summary>
        [System.Text.Json.Serialization.JsonIgnore]
        public APIResultStatus ResultStatus { get; set; } = APIResultStatus.Unknown;

        /// <summary>
        /// A code indicating the resulting status of the request. ( -3 = Unauthorized, -2 = Invalid Parameters, -1 = Error, 0 = Unknown, 1 = Completed, 2 = Failed, 3 = NoContent )
        /// </summary>
        public int ResponseCode
        {
            get
            {
                return ResultStatus.Code;
            }
        }


        /// <summary>
        /// Friendly messages to be displayed for the user.
        /// </summary>
        public List<string> ResponseMessage { get; set; } = new List<string>();

        /// <summary>
        /// The resulting data from the API request.
        /// </summary>
        public T Result { get; set; }



        /// <summary>
        /// Messages and technical information that may be relevent to the status or data returned.
        /// </summary>
        /// [System.Text.Json.Serialization.JsonIgnore]
        /// 
        [System.Text.Json.Serialization.JsonIgnore]
        public List<string> ResponseDetails { get; set; } = new List<string>();

        /// <summary>
        /// Adds a new message to the list of result messages.
        /// </summary>
        /// <param name="message">The string message to add.</param>
        /// <returns>The full list of result messages including the messages that was added.</returns>
        public List<string> AddResultMessage(string message)
        {
            ResponseDetails.Add(message);

            return ResponseDetails;
        }

        /// <summary>
        /// Adds a new message to the list of user messages.
        /// </summary>
        /// <param name="message">The string message to add.</param>
        /// <returns>The full list of user messages including the message that was added.</returns>
        public List<string> AddUserMessage(string message)
        {
            ResponseMessage.Add(message);

            return ResponseMessage;
        }

    }

    public class APIResultStatus
    {
        /// <summary>
        /// The numeric code for the status of the result.
        /// </summary>
        public int Code { get; private set; }

        /// <summary>
        /// The HttpCode for the status of the result.
        /// </summary>
        public HttpStatusCode HttpCode { get; private set; }

        /// <summary>
        /// The request is unauthorized to execute.
        /// </summary>
        public static APIResultStatus Unauthorized { get { return new APIResultStatus { Code = 401, HttpCode = HttpStatusCode.Unauthorized }; } }

        /// <summary>
        /// Indicates the parameters provided in the request are missing or invalid.
        /// </summary>
        public static APIResultStatus InvalidParameters { get { return new APIResultStatus { Code = 400, HttpCode = HttpStatusCode.BadRequest }; } }

        /// <summary>
        /// Indicates an exception occurred and the request was unsuccessful.
        /// </summary>
        public static APIResultStatus Error { get { return new APIResultStatus { Code = 500, HttpCode = HttpStatusCode.InternalServerError }; } }

        /// <summary>
        /// The result of the request is unknown or unspecified.
        /// </summary>
        public static APIResultStatus Unknown { get { return new APIResultStatus { Code = 204, HttpCode = HttpStatusCode.NoContent }; } }

        /// <summary>
        /// Indicates the request processed without issue.
        /// </summary>
        public static APIResultStatus Completed { get { return new APIResultStatus { Code = 200, HttpCode = HttpStatusCode.OK }; } }

        /// <summary>
        /// The request failed for one or more reasons.
        /// </summary>
        public static APIResultStatus Failed { get { return new APIResultStatus { Code = 200, HttpCode = HttpStatusCode.OK }; } }

        /// <summary>
        /// Indicates the request processed without issue.
        /// </summary>
        public static APIResultStatus NoContent { get { return new APIResultStatus { Code = 204, HttpCode = HttpStatusCode.NoContent }; } }

        /// <summary>
        /// Returns a result code matching the specified code value.
        /// </summary>
        /// <param name="code"></param>
        /// <returns></returns>
        public static APIResultStatus GetResultCode(int code)
        {
            foreach (PropertyInfo pi in typeof(APIResultStatus).GetProperties(BindingFlags.Static | BindingFlags.Public))
            {
                var val = pi.GetValue(pi);

                if (val.GetType() == typeof(APIResultStatus))
                {
                    APIResultStatus rt = (APIResultStatus)val;

                    if (rt.Code == code)
                    {
                        return rt;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Returns a list of all possible result codes.
        /// </summary>
        /// <returns></returns>
        public static List<APIResultStatus> GetAllResultCodes()
        {
            List<APIResultStatus> types = new List<APIResultStatus>();

            foreach (PropertyInfo pi in typeof(APIResultStatus).GetProperties(BindingFlags.Static | BindingFlags.Public))
            {
                var val = pi.GetValue(pi);

                if (val.GetType() == typeof(APIResultStatus))
                {
                    types.Add((APIResultStatus)val);
                }
            }

            return types;
        }

        /// <summary>
        /// Returns the appropriate Acknowledge enum for the provided HttpStatusCode.
        /// </summary>
        /// <param name="httpCode"></param>
        /// <returns></returns>
        public static APIResultStatus GetResultCodeForHttpCode(HttpStatusCode httpCode)
        {
            return httpCode switch
            {
                HttpStatusCode.OK => APIResultStatus.Completed,
                HttpStatusCode.BadRequest => APIResultStatus.InvalidParameters,
                HttpStatusCode.Unauthorized => APIResultStatus.Unauthorized,
                HttpStatusCode.InternalServerError => APIResultStatus.Error,
                _ => APIResultStatus.Unknown,
            };
        }
    }
}
