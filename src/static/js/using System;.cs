using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Xml;
using CXS.Payments.Core;
using CXS.Payments.EquityKeEFT.Models;
using CXS.Platform.DomainObjects;
using CXS.Platform.Globalization;
using CXS.Platform.Runtime;
using Newtonsoft.Json;

namespace CXS.Payments.EquityKeEFT
{
	// Token: 0x02000003 RID: 3
	public class EquityEFTPaymentsSystem : IPaymentSystem
	{
		// Token: 0x14000001 RID: 1
		// (add) Token: 0x0600000D RID: 13 RVA: 0x000021D4 File Offset: 0x000003D4
		// (remove) Token: 0x0600000E RID: 14 RVA: 0x0000220C File Offset: 0x0000040C
		[DebuggerBrowsable(DebuggerBrowsableState.Never)]
		public event PrintEvent OnPrintEvent;

		// Token: 0x14000002 RID: 2
		// (add) Token: 0x0600000F RID: 15 RVA: 0x00002244 File Offset: 0x00000444
		// (remove) Token: 0x06000010 RID: 16 RVA: 0x0000227C File Offset: 0x0000047C
		[DebuggerBrowsable(DebuggerBrowsableState.Never)]
		public event StatusUpdateEvent OnStatusUpdate;

		// Token: 0x14000003 RID: 3
		// (add) Token: 0x06000011 RID: 17 RVA: 0x000022B4 File Offset: 0x000004B4
		// (remove) Token: 0x06000012 RID: 18 RVA: 0x000022EC File Offset: 0x000004EC
		[DebuggerBrowsable(DebuggerBrowsableState.Never)]
		public event SignatureCaptureEvent OnSignatureCapture;

		// Token: 0x17000006 RID: 6
		// (get) Token: 0x06000013 RID: 19 RVA: 0x00002324 File Offset: 0x00000524
		public bool HardwareControlled
		{
			get
			{
				return false;
			}
		}

		// Token: 0x17000007 RID: 7
		// (get) Token: 0x06000014 RID: 20 RVA: 0x00002338 File Offset: 0x00000538
		public CardStatus Status
		{
			get
			{
				return this.m_Status;
			}
		}

		// Token: 0x17000008 RID: 8
		// (get) Token: 0x06000015 RID: 21 RVA: 0x00002350 File Offset: 0x00000550
		public Control SetupControl
		{
			get
			{
				return new SetupControl();
			}
		}

		// Token: 0x06000016 RID: 22 RVA: 0x00002368 File Offset: 0x00000568
		public AuthorizationResult Authorize(AuthorizationContext authorizeContext)
		{
			AuthorizationResult authorizationResult = new AuthorizationResult();
			LogWriter logWriter = new LogWriter();
			string text = "";
			try
			{
				AuthorizationResult authorizationResult2 = null;
				Request request = new Request();
				Random random = new Random();
				request.TillNO = "Till" + random.Next(1000, 100000).ToString();
				request.TransKey = "Trans" + random.Next(1000, 100000).ToString();
				request.CashBack = "0";
				request.Bank = "2";
				request.CashierId = "1";
				bool flag = authorizeContext.Amount != new Money(0);
				if (flag)
				{
					authorizeContext.Amount = authorizeContext.Amount;
				}
				IEnumerable<string> values = from c in Regex.Split(authorizeContext.Amount.ToString(), "[^0-9\\.]+")
				where c != "." && c.Trim() != ""
				select c;
				string text2 = string.Join(",", values);
				text2 = text2.Replace(",", "");
				request.Amount = text2;
				text = JsonConvert.SerializeObject(request);
				logWriter.LogWrite("REQUEST: " + text, "REQUEST-EQUITY");
				string text3 = this.PostKCBEFTData(text, "http://localhost:6120/feft/sale");
				bool flag2 = string.IsNullOrEmpty(text3);
				if (flag2)
				{
					authorizationResult2.AuthorizationResultType = 3;
					authorizationResult2.Message = "CANNOT CONNECT TO EQUITY SERVICE";
					return authorizationResult2;
				}
				PesaPalResponse pesaPalResponse = JsonConvert.DeserializeObject<PesaPalResponse>(text3);
				bool flag3 = pesaPalResponse.respCode.Equals("00");
				if (flag3)
				{
					try
					{
						authorizationResult.AuthorizationCode = ((pesaPalResponse.authCode != null) ? pesaPalResponse.authCode : "");
						bool flag4 = !string.IsNullOrEmpty(pesaPalResponse.pan);
						if (flag4)
						{
							authorizationResult.AdditionalInformation = pesaPalResponse.pan;
						}
						bool flag5 = !string.IsNullOrEmpty(pesaPalResponse.msg);
						if (flag5)
						{
							authorizationResult.Message = pesaPalResponse.msg;
						}
						bool flag6 = !string.IsNullOrEmpty(pesaPalResponse.rrn);
						if (flag6)
						{
							authorizationResult.PaymentReferenceNumber = pesaPalResponse.rrn;
						}
						LogWriter logWriter2 = logWriter;
						string str = "CARD PAYMENTS SAVED SUCCESSFUL: ";
						PesaPalResponse pesaPalResponse2 = pesaPalResponse;
						logWriter2.LogWrite(str + ((pesaPalResponse2 != null) ? pesaPalResponse2.ToString() : null), "SUCCESS-EQUITY");
					}
					catch (Exception ex)
					{
						LogWriter logWriter3 = logWriter;
						string str2 = "CARD PAYMENTS NOT SAVED: ";
						PesaPalResponse pesaPalResponse3 = pesaPalResponse;
						logWriter3.LogWrite(str2 + ((pesaPalResponse3 != null) ? pesaPalResponse3.ToString() : null), "ERROR-EQUITY" + ex.Message);
					}
					authorizationResult.AuthorizationResultType = 1;
				}
				else
				{
					authorizationResult.AuthorizationResultType = 3;
					authorizationResult.Message = pesaPalResponse.msg;
				}
				logWriter.LogWrite("REQUEST  ==>" + text, "AUDIT-EQUITY");
				logWriter.LogWrite("RESPONSE ==>" + text3, "AUDIT-EQUITY");
			}
			catch (Exception ex2)
			{
				authorizationResult.Message = " Msg: " + ex2.Message + " | ";
				authorizationResult.AuthorizationResultType = 3;
				LogWriter logWriter4 = logWriter;
				string[] array = new string[6];
				array[0] = " Msg: ";
				array[1] = ex2.Message;
				array[2] = " | innerEX:  ";
				int num = 3;
				Exception innerException = ex2.InnerException;
				array[num] = ((innerException != null) ? innerException.ToString() : null);
				array[4] = " | stacKTrace: ";
				array[5] = ex2.StackTrace;
				logWriter4.LogWrite(string.Concat(array), "ERROR-EQUITY");
			}
			return authorizationResult;
		}

		// Token: 0x06000017 RID: 23 RVA: 0x00002728 File Offset: 0x00000928
		public bool Initialize(XmlNode configNode, Form ownerForm)
		{
			this.m_ConfigInfo = new ConfigInfo(configNode);
			return true;
		}

		// Token: 0x06000018 RID: 24 RVA: 0x00002747 File Offset: 0x00000947
		public AuthorizationResult PreAuthorize(AuthorizationContext preAuthorizeContext)
		{
			throw new CXSException("NotAllowed");
		}

		// Token: 0x06000019 RID: 25 RVA: 0x00002754 File Offset: 0x00000954
		public AuthorizationResult Refund(AuthorizationContext refundContext)
		{
			AuthorizationResult authorizationResult = new AuthorizationResult();
			LogWriter logWriter = new LogWriter();
			string text = "";
			try
			{
				bool flag = refundContext.AuthorizationType != 1;
				if (flag)
				{
					return authorizationResult;
				}
				text = JsonConvert.SerializeObject(new EquityReversalRequest
				{
					TransKey = refundContext.InvoiceNumber,
					Bank = this.m_ConfigInfo.FortifiedBankCode,
					Amount = string.Format("{0}", refundContext.Amount)
				});
				string text2 = this.PostKCBEFTData(text, this.m_ConfigInfo.URL);
				bool flag2 = string.IsNullOrEmpty(text2);
				if (flag2)
				{
					authorizationResult.AuthorizationResultType = 3;
					authorizationResult.Message = GlobalizationManager.Instance.GetString("EquityEFTPaymentsSystem.FortifiedResponseBlank");
					return authorizationResult;
				}
				EquityReversalResponse equityReversalResponse = JsonConvert.DeserializeObject<EquityReversalResponse>(text2);
				authorizationResult.AppliedAmount = new Money(Convert.ToDecimal(equityReversalResponse.Amount));
				authorizationResult.AuthorizationCode = ((equityReversalResponse.authCode != null) ? equityReversalResponse.authCode : "");
				bool flag3 = !string.IsNullOrEmpty(equityReversalResponse.msg);
				if (flag3)
				{
					authorizationResult.Message = equityReversalResponse.msg;
				}
				bool flag4 = !string.IsNullOrEmpty(equityReversalResponse.stan);
				if (flag4)
				{
					authorizationResult.AdditionalInformation = equityReversalResponse.stan;
				}
				bool flag5 = !string.IsNullOrEmpty(equityReversalResponse.rrn);
				if (flag5)
				{
					authorizationResult.CardName = equityReversalResponse.rrn;
				}
				bool flag6 = equityReversalResponse.respCode.Equals("00");
				if (flag6)
				{
					authorizationResult.AuthorizationResultType = 1;
				}
				else
				{
					authorizationResult.AuthorizationResultType = 3;
				}
				logWriter.LogWrite("REQUEST  ==>" + text, "AUDIT-EQUITY");
				logWriter.LogWrite("RESPONSE ==>" + text2, "AUDIT-EQUITY");
			}
			catch (Exception ex)
			{
				authorizationResult.Message = " Msg: " + ex.Message + " | ";
				authorizationResult.AuthorizationResultType = 3;
				logWriter.LogWrite(" | URL: " + this.m_ConfigInfo.URL + " | req: " + text, "ERROR-EQUITY");
				LogWriter logWriter2 = logWriter;
				string[] array = new string[6];
				array[0] = " Msg: ";
				array[1] = ex.Message;
				array[2] = " | innerEX:  ";
				int num = 3;
				Exception innerException = ex.InnerException;
				array[num] = ((innerException != null) ? innerException.ToString() : null);
				array[4] = " | stacKTrace: ";
				array[5] = ex.StackTrace;
				logWriter2.LogWrite(string.Concat(array), "ERROR-EQUITY");
			}
			return authorizationResult;
		}

		// Token: 0x0600001A RID: 26 RVA: 0x000029F8 File Offset: 0x00000BF8
		public string PostCardPayments(string data, string c2buri)
		{
			string result = string.Empty;
			byte[] bytes = Encoding.UTF8.GetBytes(data);
			HttpWebRequest httpWebRequest = (HttpWebRequest)WebRequest.Create(c2buri);
			httpWebRequest.AutomaticDecompression = (DecompressionMethods.GZip | DecompressionMethods.Deflate);
			httpWebRequest.ContentLength = (long)bytes.Length;
			httpWebRequest.ContentType = "application/json";
			httpWebRequest.Method = "post";
			using (Stream requestStream = httpWebRequest.GetRequestStream())
			{
				requestStream.Write(bytes, 0, bytes.Length);
			}
			using (HttpWebResponse httpWebResponse = (HttpWebResponse)httpWebRequest.GetResponse())
			{
				using (Stream responseStream = httpWebResponse.GetResponseStream())
				{
					using (StreamReader streamReader = new StreamReader(responseStream))
					{
						result = streamReader.ReadToEnd();
					}
				}
			}
			return result;
		}

		// Token: 0x0600001B RID: 27 RVA: 0x00002B04 File Offset: 0x00000D04
		public string PostKCBEFTData(string data, string uri)
		{
			string result = string.Empty;
			byte[] bytes = Encoding.UTF8.GetBytes(data);
			HttpWebRequest httpWebRequest = (HttpWebRequest)WebRequest.Create(uri);
			httpWebRequest.AutomaticDecompression = (DecompressionMethods.GZip | DecompressionMethods.Deflate);
			httpWebRequest.ContentLength = (long)bytes.Length;
			httpWebRequest.ContentType = "application/json";
			httpWebRequest.Method = "post";
			using (Stream requestStream = httpWebRequest.GetRequestStream())
			{
				requestStream.Write(bytes, 0, bytes.Length);
			}
			using (HttpWebResponse httpWebResponse = (HttpWebResponse)httpWebRequest.GetResponse())
			{
				using (Stream responseStream = httpWebResponse.GetResponseStream())
				{
					using (StreamReader streamReader = new StreamReader(responseStream))
					{
						result = streamReader.ReadToEnd();
					}
				}
			}
			return result;
		}

		// Token: 0x0600001C RID: 28 RVA: 0x00002C10 File Offset: 0x00000E10
		public bool Shutdown()
		{
			return true;
		}

		// Token: 0x0600001D RID: 29 RVA: 0x00002C24 File Offset: 0x00000E24
		public bool Startup()
		{
			return true;
		}

		// Token: 0x04000001 RID: 1
		private ConfigInfo m_ConfigInfo = null;

		// Token: 0x04000005 RID: 5
		private CardStatus m_Status = 1;
	}
}
