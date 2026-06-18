using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Teleperformance.DataIngestion.Common
{
    public class EmailTemplate
    {
		public static string SuccessEmailTemplate()
		{
			string emailTemplate = @"<!DOCTYPE html>
					<html>
					<head>
						<meta name=""viewport"" content=""width=device-width"" />
						<title>File processed</title>

					</head>
					<body style=""font-family: 'Noto Sans', 'Open Sans', Calibri, sans-serif; font-size: 16px; background-color: #e6e6e6; color:#2e2e2e; letter-spacing: 0.3px;"">
						<br />
						<table cellpadding=""0"" cellspacing=""0"" style=""max-width: 640px; margin: 0 auto; width: 90%;"">
							<tr>
								<td style=""background-color: #c7e9ff; background-repeat: no-repeat; background-size: auto; padding: 20px 25px 10px;"">
									<table width=""10%"" style=""width: 10%"">
										<tr>
											<td>
											<img
											src=""data:image/png;base64,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""
											width=""196""
											height=""31""
											alt=""Group 48096490 1.png"">

												
											</td>
										</tr>
									</table>
									<table width=""100%"" style=""width: 100%; background-color: #fff; border-radius: 10px; padding: 10px 20px; box-shadow: 0px 2px 4px 0px rgba(0, 0, 0, 0.20);"">
										<tr>
											<td style=""text-align:center; padding-top: 20px; font-weight: bold; font-size: 18px; vertical-align: middle;"">
												<img style=""height:auto;"" src=""data:image/png;base64,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"" />
											</td>
										</tr>
										<tr>
											<td style=""font-size: 12px;"">
												<p><h4>The file has been processed successfully. Below are the details:</h4></p>
											</td>
										</tr>
										<tr>
											<td>
												<table  cellpadding=""0"" cellspacing=""0"" style=""width: 80%; text-align: left; font-size: 12px; font-weight: 600; margin: 0 auto;"">
													<tr style=""padding: 0 2rem;"">
														<th style=""background-color: #f0f0f0; border:#dddddd 1px solid; padding: 5px;"">File Name:</th>
														<td style="" padding: 5px; border:#dddddd 1px solid; color:#253788;"">#FileName</td>
													</tr>
													<tr style=""padding: 0 2rem;"">
														<th style=""background-color: #f0f0f0; border:#dddddd 1px solid; padding: 5px;"">Description:</th>
														<td style="" padding: 5px; border:#dddddd 1px solid; color:#253788;"">#File_Description</td>
													</tr>
													<tr style=""padding: 0 2rem;"">
														<th style=""background-color: #f0f0f0; border:#dddddd 1px solid; padding: 5px;"">Total Records:</th>
														<td style="" padding: 5px; border:#dddddd 1px solid; color:#253788;"">#TotalRecords</td>
													</tr>
													<tr style=""padding: 0 2rem;"">
														<th style=""background-color: #f0f0f0; border:#dddddd 1px solid; padding: 5px;"">Processed Records</th>
														<td style="" padding: 5px; border:#dddddd 1px solid; color:#253788;"">#ProcessedRecords</td>
													</tr>
													<tr style=""padding: 0 2rem;"">
														<th style=""background-color: #f0f0f0; border:#dddddd 1px solid; padding: 5px;"">Duplicate Records:</th>
														<td style="" padding: 5px; border:#dddddd 1px solid; color:#253788;"">#DuplicateRecords</td>
													</tr>
													<tr style=""padding: 0 2rem;"">
														<th style=""background-color: #f0f0f0; border:#dddddd 1px solid; padding: 5px;"">Start Time:</th>
														<td style="" padding: 5px; border:#dddddd 1px solid; color:#253788;"">#StartTime</td>
													</tr>
													<tr style=""padding: 0 2rem;"">
														<th style=""background-color: #f0f0f0; border:#dddddd 1px solid; padding: 5px;"">End Time:</th>
														<td style="" padding: 5px; border:#dddddd 1px solid; color:#253788;"">#EndTime</td>
													</tr>
													<tr style=""padding: 0 2rem;"">
														<th style=""background-color: #f0f0f0; border:#dddddd 1px solid; padding: 5px;"">Region:</th>
														<td style="" padding: 5px; border:#dddddd 1px solid; color:#253788;"">#Region</td>
													</tr>
													<tr style=""padding: 0 2rem;"">
														<th style=""background-color: #f0f0f0; border:#dddddd 1px solid; padding: 5px;"">Sub Region:</th>
														<td style="" padding: 5px; border:#dddddd 1px solid; color:#253788;"">#SubRegion</td>
													</tr>
													<tr style=""padding: 0 2rem;"">
														<th style=""background-color: #f0f0f0; border:#dddddd 1px solid; padding: 5px;"">Client:</th>
														<td style="" padding: 5px; border:#dddddd 1px solid; color:#253788;"">#Client</td>
													</tr>
												</table>
											</td>
										</tr>
										<tr>
											<td style=""font-size: 12px;"">
												<p></p>
											</td>
										</tr>
									</table>
								</td>
							</tr>
							<tr>
								<td style=""background-color: #ffffff; padding: 0px 30px; "">
									<table  cellpadding=""0"" cellspacing=""0"" style=""width: 100%"">
										<tr>
						
										<div style=""text-align: center;"">
										<p style=""color: #FF7575; font-size: 14px; font-style: italic"">
										<b>**DO NOT REPLY TO THIS EMAIL**</b> <br />
										This email was generated automatically and does not accept replies.
										</p>
										</div>

										</tr>
									</table>
								</td>
							</tr>
							<tr>
								<td style=""background-color:#E6E7F0; text-align:center; padding: 10px;"">
												<img src=""data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAACMAAAAjCAYAAAAe2bNZAAAACXBIWXMAAAsTAAALEwEAmpwYAAAAAXNSR0IArs4c6QAAAARnQU1BAACxjwv8YQUAAAE+SURBVHgB7ZUhb8JAGIbfbKiZkWXZ1DKSqf2AmamKqdkZ3LJ/MDt3+xebWc4uQWDwKDwGRQLBISAkBAu8TSGB8pUeTa9XSJ/kMd/1rm/63V2BgiPgHOnj0Xd6Ry/pAA5RdLHhiP7SChygsB1mM9TrvolnyI4r2qBvyEGYNT+0LA2UIiY8C7WWULulD6HaI/ZzTT8RtNOIGrb7PYl4TkPeH3EOpcWi2vRB/2GPm5VGYaa0ajnQRbhQiplQXT3zgvTZebfJafJb9of0mSFlNJJt4J60mIt7xqcpFV2E8dvzLQ24CPNF+7CAhvk+mSO4ea2hDYM06VPcYnH3TBLadEy7tEPrsNSWMBq7X8FDQlwdbZEiTBS5CnPIaboXamU4QsHsTvGQEQo5CmMSyEPGKFgIk/R3oBBc8RVhrI+CU2YJDP2AmxrSn58AAAAASUVORK5CYII="" />
								</td>
							</tr>
						</table>
					</body>
				</html>";
			return emailTemplate;
		}

        public static string MultisheetSuccessEmailTemplate()
        {
            string emailTemplate = @"<!DOCTYPE html>
					<html>
					<head>
						<meta name=""viewport"" content=""width=device-width"" />
						<title>File processed</title>

					</head>
					<body style=""font-family: 'Noto Sans', 'Open Sans', Calibri, sans-serif; font-size: 16px; background-color: #e6e6e6; color:#2e2e2e; letter-spacing: 0.3px;"">
						<br />
						<table cellpadding=""0"" cellspacing=""0"" style=""max-width: 640px; margin: 0 auto; width: 90%;"">
							<tr>
								<td style=""background-color: #c7e9ff; background-repeat: no-repeat; background-size: auto; padding: 20px 25px 10px;"">
									<table width=""10%"" style=""width: 10%"">
										<tr>
											<td>
											<img
											src=""data:image/png;base64,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""
											width=""196""
											height=""31""
											alt=""Group 48096490 1.png"">

												
											</td>
										</tr>
									</table>
									<table width=""100%"" style=""width: 100%; background-color: #fff; border-radius: 10px; padding: 10px 20px; box-shadow: 0px 2px 4px 0px rgba(0, 0, 0, 0.20);"">
										<tr>
											<td style=""text-align:center; padding-top: 20px; font-weight: bold; font-size: 18px; vertical-align: middle;"">
												<img style=""height:auto;"" src=""data:image/png;base64,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"" />
											</td>
										</tr>
										<tr>
											<td style=""font-size: 12px;"">
												<p><h4>The file has been processed successfully. Below are the details:</h4></p>
											</td>
										</tr>
										<tr>
											<td>
												<table  cellpadding=""0"" cellspacing=""0"" style=""width: 80%; text-align: left; font-size: 12px; font-weight: 600; margin: 0 auto;"">
													<tr style=""padding: 0 2rem;"">
														<th style=""background-color: #f0f0f0; border:#dddddd 1px solid; padding: 5px;"">File Name:</th>
														<td style="" padding: 5px; border:#dddddd 1px solid; color:#253788;"">#FileName</td>
													</tr>
													<tr style=""padding: 0 2rem;"">
														<th style=""background-color: #f0f0f0; border:#dddddd 1px solid; padding: 5px;"">Description:</th>
														<td style="" padding: 5px; border:#dddddd 1px solid; color:#253788;"">#File_Description</td>
													</tr>
													<tr style=""padding: 0 2rem;"">
														<th style=""background-color: #f0f0f0; border:#dddddd 1px solid; padding: 5px;"">Region:</th>
														<td style="" padding: 5px; border:#dddddd 1px solid; color:#253788;"">#Region</td>
													</tr>
													<tr style=""padding: 0 2rem;"">
														<th style=""background-color: #f0f0f0; border:#dddddd 1px solid; padding: 5px;"">Sub Region:</th>
														<td style="" padding: 5px; border:#dddddd 1px solid; color:#253788;"">#SubRegion</td>
													</tr>
													<tr style=""padding: 0 2rem;"">
														<th style=""background-color: #f0f0f0; border:#dddddd 1px solid; padding: 5px;"">Client:</th>
														<td style="" padding: 5px; border:#dddddd 1px solid; color:#253788;"">#Client</td>
													</tr>
                                                    #tabDetails
                                                   
													
												</table>
											</td>
										</tr>
										<tr>
											<td style=""font-size: 12px;"">
												<p></p>
											</td>
										</tr>
									</table>
								</td>
							</tr>
							<tr>
								<td style=""background-color: #ffffff; padding: 0px 30px; "">
									<table  cellpadding=""0"" cellspacing=""0"" style=""width: 100%"">
										<tr>
						
										<div style=""text-align: center;"">
										<p style=""color: #FF7575; font-size: 14px; font-style: italic"">
										<b>**DO NOT REPLY TO THIS EMAIL**</b> <br />
										This email was generated automatically and does not accept replies.
										</p>
										</div>

										</tr>
									</table>
								</td>
							</tr>
							<tr>
								<td style=""background-color:#E6E7F0; text-align:center; padding: 10px;"">
												<img src=""data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAACMAAAAjCAYAAAAe2bNZAAAACXBIWXMAAAsTAAALEwEAmpwYAAAAAXNSR0IArs4c6QAAAARnQU1BAACxjwv8YQUAAAE+SURBVHgB7ZUhb8JAGIbfbKiZkWXZ1DKSqf2AmamKqdkZ3LJ/MDt3+xebWc4uQWDwKDwGRQLBISAkBAu8TSGB8pUeTa9XSJ/kMd/1rm/63V2BgiPgHOnj0Xd6Ry/pAA5RdLHhiP7SChygsB1mM9TrvolnyI4r2qBvyEGYNT+0LA2UIiY8C7WWULulD6HaI/ZzTT8RtNOIGrb7PYl4TkPeH3EOpcWi2vRB/2GPm5VGYaa0ajnQRbhQiplQXT3zgvTZebfJafJb9of0mSFlNJJt4J60mIt7xqcpFV2E8dvzLQ24CPNF+7CAhvk+mSO4ea2hDYM06VPcYnH3TBLadEy7tEPrsNSWMBq7X8FDQlwdbZEiTBS5CnPIaboXamU4QsHsTvGQEQo5CmMSyEPGKFgIk/R3oBBc8RVhrI+CU2YJDP2AmxrSn58AAAAASUVORK5CYII="" />
								</td>
							</tr>
						</table>
					</body>
				</html>";
            return emailTemplate;
        }


		public static string MultisheetSuccessTabDetailsTemplate()
		{
			string eamilTemplate = @"<tr style=""padding: 0 2rem;"">
														<th style=""background-color: #f0f0f0; border:#dddddd 1px solid; padding: 5px;"">Tab Name:</th>
														<td style="" padding: 5px; border:#dddddd 1px solid; color:#253788;"">#TabName</td>
													</tr>
													<tr style=""padding: 0 2rem;"">
														<th style=""background-color: #f0f0f0; border:#dddddd 1px solid; padding: 5px;"">Total Records:</th>
														<td style="" padding: 5px; border:#dddddd 1px solid; color:#253788;"">#TotalRecords</td>
													</tr>
													<tr style=""padding: 0 2rem;"">
														<th style=""background-color: #f0f0f0; border:#dddddd 1px solid; padding: 5px;"">Processed Records</th>
														<td style="" padding: 5px; border:#dddddd 1px solid; color:#253788;"">#ProcessedRecords</td>
													</tr>
													<tr style=""padding: 0 2rem;"">
														<th style=""background-color: #f0f0f0; border:#dddddd 1px solid; padding: 5px;"">Duplicate Records:</th>
														<td style="" padding: 5px; border:#dddddd 1px solid; color:#253788;"">#DuplicateRecords</td>
													</tr>
													<tr style=""padding: 0 2rem;"">
														<th style=""background-color: #f0f0f0; border:#dddddd 1px solid; padding: 5px;"">Start Time:</th>
														<td style="" padding: 5px; border:#dddddd 1px solid; color:#253788;"">#StartTime</td>
													</tr>
													<tr style=""padding: 0 2rem;"">
														<th style=""background-color: #f0f0f0; border:#dddddd 1px solid; padding: 5px;"">End Time:</th>
														<td style="" padding: 5px; border:#dddddd 1px solid; color:#253788;"">#EndTime</td>
													</tr>";
			return eamilTemplate;
		}

        public static string FailureEmailTemplate()
        {
				string emailTemplate = @"<!DOCTYPE html>

					<html>
					<head>
						<meta name=""viewport"" content=""width=device-width"" />
						<title>File Process Failed</title>

					</head>
					<body style=""font-family: 'Noto Sans', 'Open Sans', Calibri, sans-serif; font-size: 16px; background-color: #e6e6e6; color:#2e2e2e; letter-spacing: 0.3px;"">
						<br />
						<table cellpadding=""0"" cellspacing=""0"" style=""max-width: 640px; margin: 0 auto; width: 90%;"">
							<tr>
								<td style=""background-color: #c7e9ff; background-repeat: no-repeat; background-size: auto; padding: 20px 25px 10px;"">
									<table width=""10%"" style=""width: 10%"">
										<tr>
											<td>
							
												<img
												src=""data:image/png;base64,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""
												width=""196""
												height=""31""
												alt=""Group 48096490 1.png"">
								
											</td>
										</tr>
									</table>
									<table width=""100%"" style=""width: 100%; background-color: #fff; border-radius: 10px; padding: 10px 20px; box-shadow: 0px 2px 4px 0px rgba(0, 0, 0, 0.20);"">
										<tr>
											<td style=""text-align:center; padding-top: 20px; font-weight: bold; font-size: 18px; vertical-align: middle;"">
												<img
												src=""data:image/png;base64,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""
												width=""196""
												height=""31""
												alt=""Group 48096491-@1x.png"">

                                        </td>
										</tr>
										<tr>
											<td style=""font-size: 12px;"">
												<p><h4>Unfortunately, the file could not be processed successfully due to an error:</h4></p>
                                                <p style=""color:red;""><b> #Error </b></p>
											</td>
										</tr>
										<tr>
											<td>
											<table  cellpadding=""0"" cellspacing=""0"" style=""width: 80%; text-align: left; font-size: 12px; font-weight: 600; margin: 0 auto;"">
													<tr style=""padding: 0 2rem;"">
														<th style=""background-color: #f0f0f0; border:#dddddd 1px solid; padding: 5px;"">File Name:</th>
														<td style="" padding: 5px; border:#dddddd 1px solid; color:#253788;"">#FileName</td>
													</tr>
													<tr style=""padding: 0 2rem;"">
														<th style=""background-color: #f0f0f0; border:#dddddd 1px solid; padding: 5px;"">Description:</th>
														<td style="" padding: 5px; border:#dddddd 1px solid; color:#253788;"">#File_Description</td>
													</tr>
													<tr style=""padding: 0 2rem;"">
														<th style=""background-color: #f0f0f0; border:#dddddd 1px solid; padding: 5px;"">Total Records:</th>
														<td style="" padding: 5px; border:#dddddd 1px solid; color:#253788;"">#TotalRecords</td>
													</tr>
													<tr style=""padding: 0 2rem;"">
														<th style=""background-color: #f0f0f0; border:#dddddd 1px solid; padding: 5px;"">Processed Records</th>
														<td style="" padding: 5px; border:#dddddd 1px solid; color:#253788;"">#ProcessedRecords</td>
													</tr>
													<tr style=""padding: 0 2rem;"">
														<th style=""background-color: #f0f0f0; border:#dddddd 1px solid; padding: 5px;"">Duplicate Records:</th>
														<td style="" padding: 5px; border:#dddddd 1px solid; color:#253788;"">#DuplicateRecords</td>
													</tr>
													<tr style=""padding: 0 2rem;"">
														<th style=""background-color: #f0f0f0; border:#dddddd 1px solid; padding: 5px;"">Start Time:</th>
														<td style="" padding: 5px; border:#dddddd 1px solid; color:#253788;"">#StartTime</td>
													</tr>
													<tr style=""padding: 0 2rem;"">
														<th style=""background-color: #f0f0f0; border:#dddddd 1px solid; padding: 5px;"">End Time:</th>
														<td style="" padding: 5px; border:#dddddd 1px solid; color:#253788;"">#EndTime</td>
													</tr>
													<tr style=""padding: 0 2rem;"">
														<th style=""background-color: #f0f0f0; border:#dddddd 1px solid; padding: 5px;"">Region:</th>
														<td style="" padding: 5px; border:#dddddd 1px solid; color:#253788;"">#Region</td>
													</tr>
													<tr style=""padding: 0 2rem;"">
														<th style=""background-color: #f0f0f0; border:#dddddd 1px solid; padding: 5px;"">Sub Region:</th>
														<td style="" padding: 5px; border:#dddddd 1px solid; color:#253788;"">#SubRegion</td>
													</tr>
													<tr style=""padding: 0 2rem;"">
														<th style=""background-color: #f0f0f0; border:#dddddd 1px solid; padding: 5px;"">Client:</th>
														<td style="" padding: 5px; border:#dddddd 1px solid; color:#253788;"">#Client</td>
													</tr>
												<tr style=""padding: 0 2rem;"">
																<th style=""background-color: #f0f0f0; border:#dddddd 1px solid; padding: 5px;"">Status:</th>
																<td style="" padding: 5px; border:#dddddd 1px solid; color:#253788;"">#Status</td>
												</tr>
							                   	<tr style=""padding: 0 2rem;"">
														<th style=""background-color: #f0f0f0; border:#dddddd 1px solid; padding: 5px;"">Processing Stage:</th>
														<td style=""padding: 5px; border:#dddddd 1px solid; color:#253788;"">#Stage</td>
												</tr>
	                                            <tr style=""padding: 0 2rem;"">
														<th style=""background-color: #f0f0f0; border:#dddddd 1px solid; padding: 5px;"">FileId:</th>
														<td style=""padding: 5px; border:#dddddd 1px solid; color:#253788;"">#BackupFileId</td>
												</tr>
												</table>
											</td>
										</tr>
										<tr>
											<td style=""font-size: 12px;"">
												<p><h4>#fileUrlText</h4></p>
											</td>
										</tr>
									</table>
								</td>
							</tr>
							<tr>
								<td style=""background-color: #ffffff; padding: 0px 30px; "">
									<table  cellpadding=""0"" cellspacing=""0"" style=""width: 100%"">
										<tr>
						
										<div style=""text-align: center;"">
										<p style=""color: #FF7575; font-size: 14px; font-style: italic"">
										<b>**DO NOT REPLY TO THIS EMAIL**</b> <br />
										This email was generated automatically and does not accept replies.
										</p>
										</div>

										</tr>
									</table>
								</td>
							</tr>
							<tr>
								<td style=""background-color:#E6E7F0; text-align:center; padding: 10px;"">
												<img src=""data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAACMAAAAjCAYAAAAe2bNZAAAACXBIWXMAAAsTAAALEwEAmpwYAAAAAXNSR0IArs4c6QAAAARnQU1BAACxjwv8YQUAAAE+SURBVHgB7ZUhb8JAGIbfbKiZkWXZ1DKSqf2AmamKqdkZ3LJ/MDt3+xebWc4uQWDwKDwGRQLBISAkBAu8TSGB8pUeTa9XSJ/kMd/1rm/63V2BgiPgHOnj0Xd6Ry/pAA5RdLHhiP7SChygsB1mM9TrvolnyI4r2qBvyEGYNT+0LA2UIiY8C7WWULulD6HaI/ZzTT8RtNOIGrb7PYl4TkPeH3EOpcWi2vRB/2GPm5VGYaa0ajnQRbhQiplQXT3zgvTZebfJafJb9of0mSFlNJJt4J60mIt7xqcpFV2E8dvzLQ24CPNF+7CAhvk+mSO4ea2hDYM06VPcYnH3TBLadEy7tEPrsNSWMBq7X8FDQlwdbZEiTBS5CnPIaboXamU4QsHsTvGQEQo5CmMSyEPGKFgIk/R3oBBc8RVhrI+CU2YJDP2AmxrSn58AAAAASUVORK5CYII="" />
								</td>
							</tr>
						</table>
					</body>
					</html>";
					return emailTemplate;
        }

        public static string MultisheetFailureEmailTemplate()
        {
            string emailTemplate = @"<!DOCTYPE html>

					<html>
					<head>
						<meta name=""viewport"" content=""width=device-width"" />
						<title>File Process Failed</title>

					</head>
					<body style=""font-family: 'Noto Sans', 'Open Sans', Calibri, sans-serif; font-size: 16px; background-color: #e6e6e6; color:#2e2e2e; letter-spacing: 0.3px;"">
						<br />
						<table cellpadding=""0"" cellspacing=""0"" style=""max-width: 640px; margin: 0 auto; width: 90%;"">
							<tr>
								<td style=""background-color: #c7e9ff; background-repeat: no-repeat; background-size: auto; padding: 20px 25px 10px;"">
									<table width=""10%"" style=""width: 10%"">
										<tr>
											<td>
							
												<img
												src=""data:image/png;base64,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""
												width=""196""
												height=""31""
												alt=""Group 48096490 1.png"">
								
											</td>
										</tr>
									</table>
									<table width=""100%"" style=""width: 100%; background-color: #fff; border-radius: 10px; padding: 10px 20px; box-shadow: 0px 2px 4px 0px rgba(0, 0, 0, 0.20);"">
										<tr>
											<td style=""text-align:center; padding-top: 20px; font-weight: bold; font-size: 18px; vertical-align: middle;"">
												<img
												src=""data:image/png;base64,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""
												width=""196""
												height=""31""
												alt=""Group 48096491-@1x.png"">

                                        </td>
										</tr>
										<tr>
											<td style=""font-size: 12px;"">
												<p><h4>Unfortunately, the file could not be processed successfully due to an error</h4></p>
                                               
											</td>
										</tr>
										<tr>
											<td>
											<table  cellpadding=""0"" cellspacing=""0"" style=""width: 80%; text-align: left; font-size: 12px; font-weight: 600; margin: 0 auto;"">
													<tr style=""padding: 0 2rem;"">
														<th style=""background-color: #f0f0f0; border:#dddddd 1px solid; padding: 5px;"">File Name:</th>
														<td style="" padding: 5px; border:#dddddd 1px solid; color:#253788;"">#FileName</td>
													</tr>
													<tr style=""padding: 0 2rem;"">
														<th style=""background-color: #f0f0f0; border:#dddddd 1px solid; padding: 5px;"">Description:</th>
														<td style="" padding: 5px; border:#dddddd 1px solid; color:#253788;"">#File_Description</td>
													</tr>
                                                   <tr style=""padding: 0 2rem;"">
														<th style=""background-color: #f0f0f0; border:#dddddd 1px solid; padding: 5px;"">Region:</th>
														<td style="" padding: 5px; border:#dddddd 1px solid; color:#253788;"">#Region</td>
													</tr>
													<tr style=""padding: 0 2rem;"">
														<th style=""background-color: #f0f0f0; border:#dddddd 1px solid; padding: 5px;"">Sub Region:</th>
														<td style="" padding: 5px; border:#dddddd 1px solid; color:#253788;"">#SubRegion</td>
													</tr>
													<tr style=""padding: 0 2rem;"">
														<th style=""background-color: #f0f0f0; border:#dddddd 1px solid; padding: 5px;"">Client:</th>
														<td style="" padding: 5px; border:#dddddd 1px solid; color:#253788;"">#Client</td>
													</tr>
												    #tabDetails
												</table>
											</td>
										</tr>
										<tr>
											<td style=""font-size: 12px;"">
												<p><h4>#fileUrlText</h4></p>
											</td>
										</tr>
									</table>
								</td>
							</tr>
							<tr>
								<td style=""background-color: #ffffff; padding: 0px 30px; "">
									<table  cellpadding=""0"" cellspacing=""0"" style=""width: 100%"">
										<tr>
						
										<div style=""text-align: center;"">
										<p style=""color: #FF7575; font-size: 14px; font-style: italic"">
										<b>**DO NOT REPLY TO THIS EMAIL**</b> <br />
										This email was generated automatically and does not accept replies.
										</p>
										</div>

										</tr>
									</table>
								</td>
							</tr>
							<tr>
								<td style=""background-color:#E6E7F0; text-align:center; padding: 10px;"">
												<img src=""data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAACMAAAAjCAYAAAAe2bNZAAAACXBIWXMAAAsTAAALEwEAmpwYAAAAAXNSR0IArs4c6QAAAARnQU1BAACxjwv8YQUAAAE+SURBVHgB7ZUhb8JAGIbfbKiZkWXZ1DKSqf2AmamKqdkZ3LJ/MDt3+xebWc4uQWDwKDwGRQLBISAkBAu8TSGB8pUeTa9XSJ/kMd/1rm/63V2BgiPgHOnj0Xd6Ry/pAA5RdLHhiP7SChygsB1mM9TrvolnyI4r2qBvyEGYNT+0LA2UIiY8C7WWULulD6HaI/ZzTT8RtNOIGrb7PYl4TkPeH3EOpcWi2vRB/2GPm5VGYaa0ajnQRbhQiplQXT3zgvTZebfJafJb9of0mSFlNJJt4J60mIt7xqcpFV2E8dvzLQ24CPNF+7CAhvk+mSO4ea2hDYM06VPcYnH3TBLadEy7tEPrsNSWMBq7X8FDQlwdbZEiTBS5CnPIaboXamU4QsHsTvGQEQo5CmMSyEPGKFgIk/R3oBBc8RVhrI+CU2YJDP2AmxrSn58AAAAASUVORK5CYII="" />
								</td>
							</tr>
						</table>
					</body>
					</html>";
            return emailTemplate;
        }

        public static string MultisheetFailureTabDetailsTemplate()
        {
            string eamilTemplate = @"<tr style=""padding: 0 2rem;"">
														<th style=""background-color: #f0f0f0; border:#dddddd 1px solid; padding: 5px;"">Tab Name:</th>
														<td style="" padding: 5px; border:#dddddd 1px solid; color:#253788;""><b>#TabName</b></td>
													</tr>
                                                    <tr style=""padding: 0 2rem;"">
														<th style=""background-color: #f0f0f0; border:#dddddd 1px solid; padding: 5px;"">Total Records:</th>
														<td style="" padding: 5px; border:#dddddd 1px solid; color:#253788;"">#TotalRecords</td>
													</tr>
													<tr style=""padding: 0 2rem;"">
														<th style=""background-color: #f0f0f0; border:#dddddd 1px solid; padding: 5px;"">Processed Records</th>
														<td style="" padding: 5px; border:#dddddd 1px solid; color:#253788;"">#ProcessedRecords</td>
													</tr>
													<tr style=""padding: 0 2rem;"">
														<th style=""background-color: #f0f0f0; border:#dddddd 1px solid; padding: 5px;"">Duplicate Records:</th>
														<td style="" padding: 5px; border:#dddddd 1px solid; color:#253788;"">#DuplicateRecords</td>
													</tr>
													<tr style=""padding: 0 2rem;"">
														<th style=""background-color: #f0f0f0; border:#dddddd 1px solid; padding: 5px;"">Start Time:</th>
														<td style="" padding: 5px; border:#dddddd 1px solid; color:#253788;"">#StartTime</td>
													</tr>
													<tr style=""padding: 0 2rem;"">
														<th style=""background-color: #f0f0f0; border:#dddddd 1px solid; padding: 5px;"">End Time:</th>
														<td style="" padding: 5px; border:#dddddd 1px solid; color:#253788;"">#EndTime</td>
													</tr>
													

							                   	<tr style=""padding: 0 2rem;"">
														<th style=""background-color: #f0f0f0; border:#dddddd 1px solid; padding: 5px;"">Processing Stage:</th>
														<td style=""padding: 5px; border:#dddddd 1px solid; color:#253788;"">#Stage</td>
												</tr>
	                                            <tr style=""padding: 0 2rem;"">
														<th style=""background-color: #f0f0f0; border:#dddddd 1px solid; padding: 5px;"">FileId:</th>
														<td style=""padding: 5px; border:#dddddd 1px solid; color:#253788;"">#BackupFileId</td>
												</tr>
                                               <tr style=""padding: 0 2rem;"">
														<th style=""background-color: #f0f0f0; border:#dddddd 1px solid; padding: 5px;"">Error:</th>
														<td style=""padding: 5px; border:#dddddd 1px solid; color:#253788;""><b>#Error </b></td>
												</tr>";
            return eamilTemplate;
        }


        public static string SuccessEmailTemplateToLandingLayer()
        {
            string emailTemplate = @"<!DOCTYPE html>
					<html>
					<head>
						<meta name=""viewport"" content=""width=device-width"" />
						<title>File processed</title>

					</head>
					<body style=""font-family: 'Noto Sans', 'Open Sans', Calibri, sans-serif; font-size: 16px; background-color: #e6e6e6; color:#2e2e2e; letter-spacing: 0.3px;"">
						<br />
						<table cellpadding=""0"" cellspacing=""0"" style=""max-width: 640px; margin: 0 auto; width: 90%;"">
							<tr>
								<td style=""background-color: #c7e9ff; background-repeat: no-repeat; background-size: auto; padding: 20px 25px 10px;"">
									<table width=""10%"" style=""width: 10%"">
										<tr>
											<td>
											<img
											src=""data:image/png;base64,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""
											width=""196""
											height=""31""
											alt=""Group 48096490 1.png"">

												
											</td>
										</tr>
									</table>
									<table width=""100%"" style=""width: 100%; background-color: #fff; border-radius: 10px; padding: 10px 20px; box-shadow: 0px 2px 4px 0px rgba(0, 0, 0, 0.20);"">
										<tr>
											<td style=""text-align:center; padding-top: 20px; font-weight: bold; font-size: 18px; vertical-align: middle;"">
												<img style=""height:auto;"" src=""data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAoAAAAAuCAYAAAC4RKiUAAAACXBIWXMAAAsTAAALEwEAmpwYAAAAAXNSR0IArs4c6QAAAARnQU1BAACxjwv8YQUAAAwSSURBVHgB7d1PcBPXHQfw364kCxswMm6AGFNEoJNMhz9mTMmhkyAP6SGHTk1P7XRSyxdO7WBunV4Ql05vsYdMD7lgl/bSmRYzPSYdxCRtE8bUNn8ybUoSmQTDlECEMTL6t+rvJ781j/XKsmXJrOzvZ0YjafW8+540ZL75vbe7BgFA3YvFCuZfJ6+suzc9E9yQDQbzvoyZo7zPT768L99gTQfS6dyGxvTku4dSBAAAa55BAFC3Oo+PND36JtOSD1JjwPSZ5dpnrbwVTPunky3mFMIgAMDahQAIUIck+CWnHrcGGtY1UoUKBcqmfak7icGuJwQAAGsKAiBAHZGp3j9MjLb689kWqpJsIZf87Nz3/0cAALBmIAAC1Amu+gVS0+m2gt8fpCqTauCLXx7+Mh43cgQAAKseAiBAHZDwNz2TbzcMClCNIAQCAKwdZReNA8DzJdO+tQ5/QvZ/Z8flHXI8AgCAVQ3/oQfwuD8l4ltqHf5scpzBG/EtBAAAqxqmgAE8bM/PPmr2+4xtzu3ffenhK0Ez30xVMHpz82XnNnNDavKT33VNEwAArEoIgAAe9vJbH+9yVv+6Dn51zu+zDlOV5PLm5Yuj7W/p22Q94E93H56IxQyLAABg1fETAHiSVP/cpn4l/GWy5vmpx8G/0DI1r0//uCFgHXNul+P++dMPN/HLb6hGOjo6IvwU4scYS6htHfKkmgzz9iSBZ/HP1U2zv2GSf6thAoC6gQAI4FE+02gt9ZllmbdHb75wmZbptX2Tr/LeXD9LWdYGqiAA7tu372ypzwqFwtD169fjEvQsy7oo2wzDGOSnXnnN2yRQnJLXpmkm+ClOVbB3795+Ps4m+z2/nlDHiHNwiRNUhH+vt/kpzI8EP4oBkH9/+Q1/pJpcuHbtGoIhgAchAAJ40Jtv/jf4hfFgRU78KEXuMtJ2fKRpqbeM43AVXeDjS+o5obWfoBrjoCeBJOzczgHm1P79+xP8+UFUG6uDf88wP0XVW/ltEQABPAgBEMCDbrTcbWqihrLtZD2g23bnmr5KtU7PBCe5GEiVSXDFL65v8Pl8CXmWsMVFwF38MiTzv7RyktwnCSQhDioy1RyWB2+TSlYvwbLxd5nk79Z+i1AN4FEIgAAe1GgZi7rHb62v45RZt176UdE6QA4CEzz95xqqOPyF8vl8caqXpwwXNU0of8MVux7ebzG4ccgY48rdpSWuPUvafVL7+4JfhnifMm3Zq/pTnMLk/cevXr06xO+jpKY0+W+Paf2J8Bh6aDZESugZlvYl+t6h+i5tQ259X+z4nO3UsePc9oK2ltKtzTi3GbTb2NT4jpQbhzaGDtW3AX6/YMCTMEgA4EkIgAAelDGNwGLu9/a3Cip9r++f/PXtBxuHPvtq4+2yjbPZqt92TglpU8Vlpwk5e4TVmsGwVl2K8LY+nsKNcWA5TUskVUgOP2O8vwjNnshQpCqDUQlrvO8wv47Jdn5vT1/TgQMHzvKxo1pfRLf0hYNRlx6yeNsJbtuv9q33vdse92LHV6Jd8dgq7NlrKUfd2kiA4+ddal8hta8OelY3fy8RPbxLKOa257UxSN+ipE3la+ZCH1d8EQABPAoXggbwoGAN7vcrjhy4/dtgQ65na2j6jcW09xXyPvIADjdSLQzLaw5YfRKy+KUd+mLqjOIlkQCk1quJeUFFBcMYzU4bj0vVS7ZzIOvj91HVr0uqH3bFLMxVwbPaMWT//doxBqS9TENL1Y5cxieflxqfCo12uwFpx49emWrn/Q2pY0bsNnKCjWojlcshfo7Zx5T1j/Q0/Nn7Oqn+LirBU/su3tZeD6q+JWh+eHSGPgRAAI9CBRCgjv3g0K3/uG1/b+TbL8vztuZU8+6dD3r+fq39jIS/hkD+2EzGf+Yf118cWsz+LdNX8f8kclA4wiGioG/jStaSrz2qql5RtU+ZwhxQH8WlusbPIRWM4ovYXUgFm01qnyG131IVyDFV0dODzAn1nOAqWcTeyPuVpx4JjjJdKmsbpYJnfy7BlTe5Ta2G9PGNj4/HnOPjgNfjMj75bhPqLOZBcsH7Dcm6S1WRdI6xu7gTDrE8Drufca72yfR3hLcfUf2Tqd+w+vwC98+eQu+3p9Ad+0XoA6gDCIAAdexJ2v/OQp+Hdzw62hS0fnG089Yx06DtEv4+vNr2DtWXsP2CQ0mEA8pF7bOQ2t5BiyPtTzm2yTSw6xSyVMT08KfCWli9HdfbqiqcBDXiKqD0Z4y3HbCnYd3Cn71bbR/O8RXxMXep/gyryp2Mo0+CI09HSzXxtDbtLJVKeS1TwDJ1281tBvU2KlSH5TVv36kfU02B22fzylj0qeS4/UK+F95vXK2f1KECCFAHEAABPKjQQFkjU/7+vx9cazuz0Ocf3dh6/rX9k9vXNeR+WUn4M618xXcCkcoSV5+itEyOABLWpm0ros5MlmCS5P5dKHMSScLxXl8rWIuLZC84PglwHN4OyjSzvXZRpqPlwduPyVjUGdZdHPBi/HkxkNptuKJ4kquw/fRs1a7cdzrX1nlSB79/SAtDAATwKARAAA/KpHJW0F/+n+fBPQ9cbwmn39/3Aw59B/fcu1zJhaPTuWyalsF5xmmF9BBhB5hKybRtF1VIxqOmekmrBBZxiJoLSvY6OK7YyfrB4japHpa41uCSxqe+0y5Z66emjntUf2Sd3rDWRkJhTA+C7BRvG3Qcc4CP2bfAIZNuYxQ8tp2OE03sYy95qh8AVhZOAgHwIF+wcVHX3vtWaPqc28PZrtK7hgTXNWbpOeMwFaenIaTH+bkEK3nQCrGvbShTpeokD3Lp25hqOzdNrKZu52gnriRIG59zLPr49OPJ2j8OblHtWothZ3sJY9KGZk8+EbI9pEJaQjtmuNQx1fc/b4zyN6oKOY+sXeRHDwGAZ6ECCOBBVuB+hvLNZdt9nWysygWfS8lkWyq9CHTVSNWMw4QEmOJZq/xaTjwoVrpk7R8Hqw65kwet0HQjB6LTfMwIzZ58MqouJSPr5uwTSuautcf9khMlTqjP+ritvb6ug6dxJRxG1JTtaVXBk/GM8hhdxycVPwlXar2h/L3c3q5DtS1epob3282fneVjxe0zl+lpcEtofRtQxyxeDkY7ZvGkGnXmcFxdLieuwl7x++c2CemX2/ejritYrGJy25ZlVmwBoEYQAAE86PO2Nx7tvvnPFwJlzsKtxv2AF9J+e0/qJj1/HCJiaurVvlxKccpSTT8maQXXmknljQPbSftkDEcVbIjfn9TaSrjrte+ZW6pixu36eXz2CSphmj8+nRyzm9RZvErCXm8p0898PPsyNvrxknLJGJdj9rgc8xm8z177+oNqHHL3lEvqUjbPVDb1aWI+Xi3WSQJAFSAAAnhRzLD8P/lXihqyG+g5MU1jKh43crRE6hp2YqFQltTaJbS/HaSnZ5qO6X8gIVCtX7OrYhIuJkhVqWjhPvUuok/z+lBqDaOEJ+7LsNaXxOzm+be1kxMzuK3sL6L1e1xdvmVJ41Nt7OOG3faljicVw3C570ntr9/RNzmxY1hvq76HXdy2W7WLqyAcUt9VUtunfDfyXqqTFwgAPAkLdQE8qvP4SNPjJ/l25/ajnbdG+GnKsszyd/IowzSt7XJ5GPu6gbpttw5/XkkABAAA78NJIAAedeXdQ6lsxphxbk8+Cv6KOPzJP97lPmQ/j5/4fuM8RqXVPwAAqA+YAgbwsFCzeTeZyuzU1wJe+XTr+/z0PtVIoUDZLYnvff0JAQDAaoUKIICHcRUwG2pquEcrKG9tvo/qHwDA6oYACOBxHAJlUf59Whn3b/7xO1MEAACrGk4CAagTr/z841Z+aqXauf/v37+6UkETAACeI1QAAeqEhLNALlP16eCslbdkvwh/AABrByqAAHWm8/hIYHom324YFKBlymaezOy4+/odrPkDAFhbEAAB6tRLx9/bFJhp3lxJEJTgZ/jaHmK9HwDA2oQACFDn5ILRVtZcn0zPNAX9/mCpdulcLh0KNqbMgPVYrjFIAACwZiEAAqwy4ejFda0NG/X1vdkftnXmYzHDIgAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAACokv8DrIC6nXUmclsAAAAASUVORK5CYII="" />
											</td>
										</tr>
										<tr>
											<td style=""font-size: 12px;"">
												<p><h4>The file has been processed successfully. Below are the details:</h4></p>
											</td>
										</tr>
										<tr>
											<td>
												<table  cellpadding=""0"" cellspacing=""0"" style=""width: 80%; text-align: left; font-size: 12px; font-weight: 600; margin: 0 auto;"">
													<tr style=""padding: 0 2rem;"">
														<th style=""background-color: #f0f0f0; border:#dddddd 1px solid; padding: 5px;"">Process Name:</th>
														<td style="" padding: 5px; border:#dddddd 1px solid; color:#253788;"">#ProcessName</td>
													</tr>
													<tr style=""padding: 0 2rem;"">
														<th style=""background-color: #f0f0f0; border:#dddddd 1px solid; padding: 5px;"">Description:</th>
														<td style="" padding: 5px; border:#dddddd 1px solid; color:#253788;"">#FileDescription</td>
													</tr>
													<tr style=""padding: 0 2rem;"">
														<th style=""background-color: #f0f0f0; border:#dddddd 1px solid; padding: 5px;"">Total Files Received:</th>
														<td style="" padding: 5px; border:#dddddd 1px solid; color:#253788;"">#TotalFiles</td>
													</tr>
													<tr style=""padding: 0 2rem;"">
														<th style=""background-color: #f0f0f0; border:#dddddd 1px solid; padding: 5px;"">Successfully Moved to Landing Layer:</th>
														<td style="" padding: 5px; border:#dddddd 1px solid; color:#253788;"">#TotalSuccessMovedFile</td>
													</tr>
													<tr style=""padding: 0 2rem;"">
														<th style=""background-color: #f0f0f0; border:#dddddd 1px solid; padding: 5px;"">Total Rejected Files:</th>
														<td style="" padding: 5px; border:#dddddd 1px solid; color:#253788;"">#TotalRejectedFile</td>
													</tr>
													
													<tr style=""padding: 0 2rem;"">
														<th style=""background-color: #f0f0f0; border:#dddddd 1px solid; padding: 5px;"">Date:</th>
														<td style="" padding: 5px; border:#dddddd 1px solid; color:#253788;"">#EndTime</td>
													</tr>
													<tr style=""padding: 0 2rem;"">
														<th style=""background-color: #f0f0f0; border:#dddddd 1px solid; padding: 5px;"">Region:</th>
														<td style="" padding: 5px; border:#dddddd 1px solid; color:#253788;"">#Region</td>
													</tr>
													<tr style=""padding: 0 2rem;"">
														<th style=""background-color: #f0f0f0; border:#dddddd 1px solid; padding: 5px;"">Sub Region:</th>
														<td style="" padding: 5px; border:#dddddd 1px solid; color:#253788;"">#SubRegion</td>
													</tr>
													<tr style=""padding: 0 2rem;"">
														<th style=""background-color: #f0f0f0; border:#dddddd 1px solid; padding: 5px;"">Client:</th>
														<td style="" padding: 5px; border:#dddddd 1px solid; color:#253788;"">#Client</td>
													</tr>
												</table>
											</td>
										</tr>
										<tr>
											<td style=""font-size: 12px;"">
												<p></p>
											</td>
										</tr>
									</table>
								</td>
							</tr>
							<tr>
								<td style=""background-color: #ffffff; padding: 0px 30px; "">
									<table  cellpadding=""0"" cellspacing=""0"" style=""width: 100%"">
										<tr>
						
										<div style=""text-align: center;"">
										<p style=""color: #FF7575; font-size: 14px; font-style: italic"">
										<b>**DO NOT REPLY TO THIS EMAIL**</b> <br />
										This email was generated automatically and does not accept replies.
										</p>
										</div>

										</tr>
									</table>
								</td>
							</tr>
							<tr>
								<td style=""background-color:#E6E7F0; text-align:center; padding: 10px;"">
												<img src=""data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAACMAAAAjCAYAAAAe2bNZAAAACXBIWXMAAAsTAAALEwEAmpwYAAAAAXNSR0IArs4c6QAAAARnQU1BAACxjwv8YQUAAAE+SURBVHgB7ZUhb8JAGIbfbKiZkWXZ1DKSqf2AmamKqdkZ3LJ/MDt3+xebWc4uQWDwKDwGRQLBISAkBAu8TSGB8pUeTa9XSJ/kMd/1rm/63V2BgiPgHOnj0Xd6Ry/pAA5RdLHhiP7SChygsB1mM9TrvolnyI4r2qBvyEGYNT+0LA2UIiY8C7WWULulD6HaI/ZzTT8RtNOIGrb7PYl4TkPeH3EOpcWi2vRB/2GPm5VGYaa0ajnQRbhQiplQXT3zgvTZebfJafJb9of0mSFlNJJt4J60mIt7xqcpFV2E8dvzLQ24CPNF+7CAhvk+mSO4ea2hDYM06VPcYnH3TBLadEy7tEPrsNSWMBq7X8FDQlwdbZEiTBS5CnPIaboXamU4QsHsTvGQEQo5CmMSyEPGKFgIk/R3oBBc8RVhrI+CU2YJDP2AmxrSn58AAAAASUVORK5CYII="" />
								</td>
							</tr>
						</table>
					</body>
				</html>";
            return emailTemplate;
        }


        public static string FailureEmailTemplateToLandingLayer()
        {
            string emailTemplate = @"<!DOCTYPE html>
					<html>
					<head>
						<meta name=""viewport"" content=""width=device-width"" />
						<title>File Process Failed</title>

					</head>
					<body style=""font-family: 'Noto Sans', 'Open Sans', Calibri, sans-serif; font-size: 16px; background-color: #e6e6e6; color:#2e2e2e; letter-spacing: 0.3px;"">
						<br />
						<table cellpadding=""0"" cellspacing=""0"" style=""max-width: 640px; margin: 0 auto; width: 90%;"">
							<tr>
								<td style=""background-color: #c7e9ff; background-repeat: no-repeat; background-size: auto; padding: 20px 25px 10px;"">
									<table width=""10%"" style=""width: 10%"">
										<tr>
											<td>
							
												<img
												src=""data:image/png;base64,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""
												width=""196""
												height=""31""
												alt=""Group 48096490 1.png"">
								
											</td>
										</tr>
									</table>
									<table width=""100%"" style=""width: 100%; background-color: #fff; border-radius: 10px; padding: 10px 20px; box-shadow: 0px 2px 4px 0px rgba(0, 0, 0, 0.20);"">
										<tr>
											<td style=""text-align:center; padding-top: 20px; font-weight: bold; font-size: 18px; vertical-align: middle;"">
												<img
												src=""data:image/png;base64,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""
												width=""196""
												height=""31""
												alt=""Group 48096491-@1x.png"">

                                        </td>
										</tr>
										<tr>
											<td style=""font-size: 12px;"">
												<p><h4>Unfortunately, the file could not be processed successfully due to an error:</h4></p>
                                                <p style=""color:red;""><b> #Error </b></p>
											</td>
										</tr>
										<tr>
											<td>
											<table  cellpadding=""0"" cellspacing=""0"" style=""width: 80%; text-align: left; font-size: 12px; font-weight: 600; margin: 0 auto;"">
													<tr style=""padding: 0 2rem;"">
														<th style=""background-color: #f0f0f0; border:#dddddd 1px solid; padding: 5px;"">Process Name:</th>
														<td style="" padding: 5px; border:#dddddd 1px solid; color:#253788;"">#ProcessName</td>
													</tr>
													<tr style=""padding: 0 2rem;"">
														<th style=""background-color: #f0f0f0; border:#dddddd 1px solid; padding: 5px;"">Description:</th>
														<td style="" padding: 5px; border:#dddddd 1px solid; color:#253788;"">#FileDescription</td>
													</tr>																										
													
													<tr style=""padding: 0 2rem;"">
														<th style=""background-color: #f0f0f0; border:#dddddd 1px solid; padding: 5px;"">Date:</th>
														<td style="" padding: 5px; border:#dddddd 1px solid; color:#253788;"">#EndTime</td>
													</tr>
													<tr style=""padding: 0 2rem;"">
														<th style=""background-color: #f0f0f0; border:#dddddd 1px solid; padding: 5px;"">Region:</th>
														<td style="" padding: 5px; border:#dddddd 1px solid; color:#253788;"">#Region</td>
													</tr>
													<tr style=""padding: 0 2rem;"">
														<th style=""background-color: #f0f0f0; border:#dddddd 1px solid; padding: 5px;"">Sub Region:</th>
														<td style="" padding: 5px; border:#dddddd 1px solid; color:#253788;"">#SubRegion</td>
													</tr>
													<tr style=""padding: 0 2rem;"">
														<th style=""background-color: #f0f0f0; border:#dddddd 1px solid; padding: 5px;"">Client:</th>
														<td style="" padding: 5px; border:#dddddd 1px solid; color:#253788;"">#Client</td>
													</tr>
												<tr style=""padding: 0 2rem;"">
																<th style=""background-color: #f0f0f0; border:#dddddd 1px solid; padding: 5px;"">Status:</th>
																<td style="" padding: 5px; border:#dddddd 1px solid; color:#253788;"">#Status</td>
												</tr>
							                   	<tr style=""padding: 0 2rem;"">
														<th style=""background-color: #f0f0f0; border:#dddddd 1px solid; padding: 5px;"">Processing Stage:</th>
														<td style=""padding: 5px; border:#dddddd 1px solid; color:#253788;"">#Stage</td>
												</tr>	                                           
												</table>
											</td>
										</tr>
										
									</table>
								</td>
							</tr>
							<tr>
								<td style=""background-color: #ffffff; padding: 0px 30px; "">
									<table  cellpadding=""0"" cellspacing=""0"" style=""width: 100%"">
										<tr>
						
										<div style=""text-align: center;"">
										<p style=""color: #FF7575; font-size: 14px; font-style: italic"">
										<b>**DO NOT REPLY TO THIS EMAIL**</b> <br />
										This email was generated automatically and does not accept replies.
										</p>
										</div>

										</tr>
									</table>
								</td>
							</tr>
							<tr>
								<td style=""background-color:#E6E7F0; text-align:center; padding: 10px;"">
												<img src=""data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAACMAAAAjCAYAAAAe2bNZAAAACXBIWXMAAAsTAAALEwEAmpwYAAAAAXNSR0IArs4c6QAAAARnQU1BAACxjwv8YQUAAAE+SURBVHgB7ZUhb8JAGIbfbKiZkWXZ1DKSqf2AmamKqdkZ3LJ/MDt3+xebWc4uQWDwKDwGRQLBISAkBAu8TSGB8pUeTa9XSJ/kMd/1rm/63V2BgiPgHOnj0Xd6Ry/pAA5RdLHhiP7SChygsB1mM9TrvolnyI4r2qBvyEGYNT+0LA2UIiY8C7WWULulD6HaI/ZzTT8RtNOIGrb7PYl4TkPeH3EOpcWi2vRB/2GPm5VGYaa0ajnQRbhQiplQXT3zgvTZebfJafJb9of0mSFlNJJt4J60mIt7xqcpFV2E8dvzLQ24CPNF+7CAhvk+mSO4ea2hDYM06VPcYnH3TBLadEy7tEPrsNSWMBq7X8FDQlwdbZEiTBS5CnPIaboXamU4QsHsTvGQEQo5CmMSyEPGKFgIk/R3oBBc8RVhrI+CU2YJDP2AmxrSn58AAAAASUVORK5CYII="" />
								</td>
							</tr>
						</table>
					</body>
					</html>";
            return emailTemplate;
        }

    }
}
