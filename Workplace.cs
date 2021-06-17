using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using Microsoft.Extensions.Logging;
using MySqlConnector;
using static System.Console;

namespace terminalServerCore {
    
    
    public enum StateType {
        Idle,
        Running,
        PowerOff
    }

    public class Workplace {
        public const string NavUrl = "http://localhost:8000/send";
        public int Oid { get; set; }
        public string Name { get; set; }
        public int DeviceOid { get; set; }
        public int ActualWorkshiftId { get; set; }
        public int PreviousWorkshiftId { get; set; }
        public DateTime LastStateDateTime { get; set; }
        public int DefaultOrder { get; set; }
        public int DefaultProduct { get; set; }
        public int WorkplaceDivisionId { get; set; }
        public int ProductionPortOid { get; set; }
        public int CountPortOid { get; set; }
        public int NokPortOid { get; set; }
        public DateTime OrderStartDate { get; set; }
        public int? OrderUserId { get; set; }
        public StateType ActualStateType { get; set; }
        public int WorkplaceIdleId { get; set; }
        
        public string Code  { get; set; }

        public Workplace() {
            Oid = Oid;
            Name = Name;
            DeviceOid = DeviceOid;
            ActualWorkshiftId = ActualWorkshiftId;
            LastStateDateTime = LastStateDateTime;
            DefaultOrder = DefaultOrder;
            DefaultProduct = DefaultProduct;
            WorkplaceDivisionId = WorkplaceDivisionId;
            ProductionPortOid = ProductionPortOid;
            CountPortOid = CountPortOid;
            OrderStartDate = OrderStartDate;
            ActualStateType = ActualStateType;
        }

        public void CloseIdleForWorkplace(DateTime dateTimeToInsert, ILogger logger) {
            if (Program.CloseOnlyAutomaticIdles.Equals("1")) {
                SendAllXmlData(logger);
                LogError("[ " + Name + " ] --INF-- Closing automatic idle: " + Program.CloseOnlyAutomaticIdles, logger);
                var dateToInsert = string.Format("{0:yyyy-MM-dd HH:mm:ss}", DateTime.Now);

                var connection = new MySqlConnection(
                    $"server={Program.IpAddress};port={Program.Port};userid={Program.Login};password={Program.Password};database={Program.Database};");
                try {
                    connection.Open();
                    var command = connection.CreateCommand();
                    command.CommandText =
                        $"UPDATE `zapsi2`.`terminal_input_idle` t SET t.`DTE` = '{dateToInsert}', t.Interval = TIME_TO_SEC(timediff('{dateToInsert}', DTS)) WHERE t.`DTE` is NULL and DeviceID={DeviceOid} and Note like 'Automatic idle'";
                    try {
                        command.ExecuteNonQuery();
                    } catch (Exception error) {
                        LogError(
                            "[ MAIN ] --ERR-- problem closing idle in database: " + error.Message + command.CommandText,
                            logger);
                    } finally {
                        command.Dispose();
                    }

                    connection.Close();
                } catch (Exception error) {
                    LogError("[ " + Name + " ] --ERR-- Problem with database: " + error.Message, logger);
                } finally {
                    connection.Dispose();
                }
            } else {
                LogError("[ " + Name + " ] --INF-- Closing idle: " + Program.CloseOnlyAutomaticIdles, logger);
                SendAllXmlData(logger);
                var dateToInsert = string.Format("{0:yyyy-MM-dd HH:mm:ss}", DateTime.Now);

                var connection = new MySqlConnection(
                    $"server={Program.IpAddress};port={Program.Port};userid={Program.Login};password={Program.Password};database={Program.Database};");
                try {
                    connection.Open();
                    var command = connection.CreateCommand();


                    command.CommandText =
                        $"UPDATE `zapsi2`.`terminal_input_idle` t SET t.`DTE` = '{dateToInsert}', t.Interval = TIME_TO_SEC(timediff('{dateToInsert}', DTS)) WHERE t.`DTE` is NULL and DeviceID={DeviceOid}";
                    try {
                        command.ExecuteNonQuery();
                    } catch (Exception error) {
                        LogError(
                            "[ MAIN ] --ERR-- problem closing idle in database: " + error.Message + command.CommandText,
                            logger);
                    } finally {
                        command.Dispose();
                    }

                    connection.Close();
                } catch (Exception error) {
                    LogError("[ " + Name + " ] --ERR-- Problem with database: " + error.Message, logger);
                } finally {
                    connection.Dispose();
                }
            }

            LogInfo("[ " + Name + " ] --INF-- Terminal_input_idle closed", logger);
        }

        private void SendAllXmlData(ILogger logger) {
            var openIdleIsTypeFour = CheckOpenIdleForTypeFour(logger);
            if (openIdleIsTypeFour) {
                var userLogin = GetUserLoginFor(logger);
                var actualOrderId = GetOrderIdFor(logger);
                var orderNo = GetOrderNo(actualOrderId, logger);
                var operationNo = GetOperationNo(actualOrderId, logger);
                var time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                var divisionName = "AL";
                if (WorkplaceDivisionId == 3) {
                    divisionName = "PL";
                }
                var orderData = CreateXml(divisionName, orderNo, operationNo, userLogin, time, "Production", "true");
                LogInfo("[ " + Name + " ] --INF-- Sending production/true XML for user: "  +userLogin, logger);
                SendXml(NavUrl, orderData, logger);
                var listOfUsers = GetAdditionalUsersFor(logger);
                foreach (var actualUserLogin in listOfUsers) {
                    var userData = CreateXml( divisionName, orderNo, operationNo, actualUserLogin, time, "Production", "false");
                    LogInfo("[ " + Name + " ] --INF-- Sending production/false XML for additional user: "  +userLogin, logger);
                    SendXml(NavUrl, userData, logger);
                }
                var loginUsers = GetLoginUsersFor(logger);
                foreach (var actualUserLogin in loginUsers) {
                    var userData = CreateXml( divisionName, orderNo, operationNo, actualUserLogin, time, "Spare", "false");
                    LogInfo("[ " + Name + " ] --INF-- Sending spare/false XML for login user: "  +userLogin, logger);
                    SendXml(NavUrl, userData, logger);
                }
            }
        }

        private string CreateXml(string divisionName, string orderNo, string operationNo, string userLogin, string time, string operationType, string initiator) {
            var data = "xml=" +
                       "<ZAPSIoperations>" +
                       "<ZAPSIoperation>" +
                       "<type>" + divisionName + "</type>" +
                       "<orderno>" + orderNo + "</orderno>" +
                       "<operationno>" + operationNo + "</operationno>" +
                       "<workcenter>" + Code + "</workcenter>" +
                       "<machinecenter>" + userLogin + "</machinecenter>" +
                       "<operationtype>" + operationType + "</operationtype>" +
                       "<initiator>" + initiator + "</initiator>" +
                       "<startdate>" + time + ".000</startdate>" +
                       "<enddate/>" +
                       "<consofmeters/>" +
                       "<motorhours/>" +
                       "<cuts/>" +
                       "<note/>" +
                       "</ZAPSIoperation>" +
                       "</ZAPSIoperations>";
            return data;
        }

        private string GetUserLoginFor(ILogger logger) {
            var userLogin = "";
            var connection = new MySqlConnection(
                $"server={Program.IpAddress};port={Program.Port};userid={Program.Login};password={Program.Password};database={Program.Database};");
            try {
                connection.Open();
                var selectQuery = $"select * from User where OID in (SELECT UserId from zapsi2.terminal_input_order where DTE is NULL and DeviceID={DeviceOid})";
                var command = new MySqlCommand(selectQuery, connection);
                try {
                    var reader = command.ExecuteReader();
                    if (reader.Read()) {
                        userLogin = Convert.ToString(reader["Login"]);
                    }

                    reader.Close();
                    reader.Dispose();
                } catch (Exception error) {
                    LogError("[ " + Name + " ] --ERR-- Problem checking active order: " + error.Message + selectQuery, logger);
                } finally {
                    command.Dispose();
                }

                connection.Close();
            } catch (Exception error) {
                LogError("[ " + Name + " ] --ERR-- Problem with database: " + error.Message, logger);
            } finally {
                connection.Dispose();
            }

            LogInfo("[ " + Name + " ] --INF-- Open order has userId: " + userLogin, logger);

            return userLogin;
        }

        private int GetUserIdFor(ILogger logger) {
            int userId = 0;
            var connection = new MySqlConnection(
                $"server={Program.IpAddress};port={Program.Port};userid={Program.Login};password={Program.Password};database={Program.Database};");
            try {
                connection.Open();
                var selectQuery = $"SELECT * from zapsi2.terminal_input_order where DTE is NULL and DeviceID={DeviceOid}";
                var command = new MySqlCommand(selectQuery, connection);
                try {
                    var reader = command.ExecuteReader();
                    while (reader.Read()) {
                        userId = Convert.ToInt32(reader["UserID"]);
                    }

                    reader.Close();
                    reader.Dispose();
                } catch (Exception error) {
                    LogError("[ " + Name + " ] --ERR-- Problem checking terminal input order users: " + error.Message + selectQuery, logger);
                } finally {
                    command.Dispose();
                }

                connection.Close();
            } catch (Exception error) {
                LogError("[ " + Name + " ] --ERR-- Problem with database: " + error.Message, logger);
            } finally {
                connection.Dispose();
            }

            LogInfo("[ " + Name + " ] --INF-- Actual terminal_input_order userid: " + userId, logger);

            return userId;
        }

        private List<string> GetLoginUsersFor(ILogger logger) {
            var listOfUsers = new List<string>();
            var connection = new MySqlConnection(
                $"server={Program.IpAddress};port={Program.Port};userid={Program.Login};password={Program.Password};database={Program.Database};");
            try {
                connection.Open();
                var selectQuery =
                    $"SELECT * from zapsi2.terminal_input_login where DTE is NULL and DeviceID={DeviceOid}";
                var command = new MySqlCommand(selectQuery, connection);
                try {
                    var reader = command.ExecuteReader();
                    while (reader.Read()) {
                        var userId = Convert.ToString(reader["Login"]);
                        listOfUsers.Add(userId);
                    }

                    reader.Close();
                    reader.Dispose();
                } catch (Exception error) {
                    LogError("[ " + Name + " ] --ERR-- Problem checking terminal input login users: " + error.Message + selectQuery, logger);
                } finally {
                    command.Dispose();
                }

                connection.Close();
            } catch (Exception error) {
                LogError("[ " + Name + " ] --ERR-- Problem with database: " + error.Message, logger);
            } finally {
                connection.Dispose();
            }

            LogInfo("[ " + Name + " ] --INF-- Open order has no of login users: " + listOfUsers.Count, logger);

            return listOfUsers;
        }

        public void SendXml(string destinationUrl, string requestXml, ILogger logger) {
            LogInfo($"[ {Name} ] --INF-- Sending XML", logger);
            HttpWebRequest request = (HttpWebRequest) WebRequest.Create(destinationUrl);
            byte[] bytes = Encoding.UTF8.GetBytes(requestXml);
            request.ContentType = "application/x-www-form-urlencoded";
            request.ContentLength = bytes.Length;
            request.Method = "POST";
            try {
                Stream requestStream = request.GetRequestStream();
                requestStream.Write(bytes, 0, bytes.Length);
                HttpWebResponse response;
                response = (HttpWebResponse) request.GetResponse();
                if (response.StatusCode == HttpStatusCode.OK) {
                    LogInfo($"[ {Name} ] --INF-- XML sent OK", logger);
                    return;
                }
                LogInfo($"[ {Name} ] --INF-- XML not sent!!!", logger);
            } catch {
                LogInfo($"[ {Name} ] --INF-- XML not sent!!!", logger);
            }
        }
        
        private string GetOperationNo(int actualOrderId, ILogger logger) {
            var operation = "error getting order operation";
            var connection = new MySqlConnection(
                $"server={Program.IpAddress};port={Program.Port};userid={Program.Login};password={Program.Password};database={Program.Database};");
            try {
                connection.Open();
                var selectQuery = $"SELECT * from zapsi2.order where OID={actualOrderId}";
                var command = new MySqlCommand(selectQuery, connection);
                try {
                    var reader = command.ExecuteReader();
                    if (reader.Read()) {
                        operation = Convert.ToString(reader["OperationNo"]);
                    }

                    reader.Close();
                    reader.Dispose();
                } catch (Exception error) {
                    LogError("[ " + Name + " ] --ERR-- Problem checking operationNo: " + error.Message + selectQuery, logger);
                } finally {
                    command.Dispose();
                }

                connection.Close();
            } catch (Exception error) {
                LogError("[ " + Name + " ] --ERR-- Problem with database: " + error.Message, logger);
            } finally {
                connection.Dispose();
            }

            LogInfo("[ " + Name + " ] --INF-- Open order has operation: " + operation, logger);
            return operation;
        }
        private string GetOrderNo(int actualOrderId, ILogger logger) {
            var orderBarcode = "error getting order barcode";
            var connection = new MySqlConnection(
                $"server={Program.IpAddress};port={Program.Port};userid={Program.Login};password={Program.Password};database={Program.Database};");
            try {
                connection.Open();
                var selectQuery = $"SELECT * from zapsi2.order where OID={actualOrderId}";
                var command = new MySqlCommand(selectQuery, connection);
                try {
                    var reader = command.ExecuteReader();
                    if (reader.Read()) {
                        orderBarcode = Convert.ToString(reader["Barcode"]);
                    }

                    reader.Close();
                    reader.Dispose();
                } catch (Exception error) {
                    LogError("[ " + Name + " ] --ERR-- Problem checking active order: " + error.Message + selectQuery, logger);
                } finally {
                    command.Dispose();
                }

                connection.Close();
            } catch (Exception error) {
                LogError("[ " + Name + " ] --ERR-- Problem with database: " + error.Message, logger);
            } finally {
                connection.Dispose();
            }

            LogInfo("[ " + Name + " ] --INF-- Open order has barcode: " + orderBarcode, logger);
            return orderBarcode;
        }
        private int GetOrderIdFor(ILogger logger) {
            var orderId = 1;
            var connection = new MySqlConnection(
                $"server={Program.IpAddress};port={Program.Port};userid={Program.Login};password={Program.Password};database={Program.Database};");
            try {
                connection.Open();
                var selectQuery = $"SELECT * from zapsi2.terminal_input_order where DTE is NULL and DeviceID={DeviceOid}";
                var command = new MySqlCommand(selectQuery, connection);
                try {
                    var reader = command.ExecuteReader();
                    if (reader.Read()) {
                        orderId = Convert.ToInt32(reader["OrderID"]);
                    }

                    reader.Close();
                    reader.Dispose();
                } catch (Exception error) {
                    LogError("[ " + Name + " ] --ERR-- Problem checking active order: " + error.Message + selectQuery, logger);
                } finally {
                    command.Dispose();
                }

                connection.Close();
            } catch (Exception error) {
                LogError("[ " + Name + " ] --ERR-- Problem with database: " + error.Message, logger);
            } finally {
                connection.Dispose();
            }

            LogInfo("[ " + Name + " ] --INF-- Open order has orderid: " + orderId, logger);

            return orderId;
        }
        
        private string GetWorkcenter(int userId, ILogger logger) {
            var userLogin = "error getting user login";
            var connection = new MySqlConnection(
                $"server={Program.IpAddress};port={Program.Port};userid={Program.Login};password={Program.Password};database={Program.Database};");
            try {
                connection.Open();
                var selectQuery = $"SELECT * from zapsi2.user where OID={userId}";
                var command = new MySqlCommand(selectQuery, connection);
                try {
                    var reader = command.ExecuteReader();
                    if (reader.Read()) {
                        userLogin = Convert.ToString(reader["Login"]);
                    }

                    reader.Close();
                    reader.Dispose();
                } catch (Exception error) {
                    LogError("[ " + Name + " ] --ERR-- Problem checking user login: " + error.Message + selectQuery, logger);
                } finally {
                    command.Dispose();
                }

                connection.Close();
            } catch (Exception error) {
                LogError("[ " + Name + " ] --ERR-- Problem with database: " + error.Message, logger);
            } finally {
                connection.Dispose();
            }

            LogInfo("[ " + Name + " ] --INF-- User has login: " + userLogin, logger);
            return userLogin;
        }
        
        private  List<string> GetAdditionalUsersFor(ILogger logger) {
            var listOfUsers = new List<string>();
            var connection = new MySqlConnection(
                $"server={Program.IpAddress};port={Program.Port};userid={Program.Login};password={Program.Password};database={Program.Database};");
            try {
                connection.Open();
                var selectQuery =
                    $"select * from User where OID in (SELECT UserId FROM zapsi2.terminal_input_order_user where TerminalInputOrderID = (SELECT OID from zapsi2.terminal_input_order where DTE is NULL and DeviceID={DeviceOid}))";
                var command = new MySqlCommand(selectQuery, connection);
                try {
                    var reader = command.ExecuteReader();
                    while (reader.Read()) {
                        var userId = Convert.ToString(reader["Login"]);
                        listOfUsers.Add(userId);
                    }

                    reader.Close();
                    reader.Dispose();
                } catch (Exception error) {
                    LogError("[ " + Name + " ] --ERR-- Problem checking terminal input order users: " + error.Message + selectQuery, logger);
                } finally {
                    command.Dispose();
                }

                connection.Close();
            } catch (Exception error) {
                LogError("[ " + Name + " ] --ERR-- Problem with database: " + error.Message, logger);
            } finally {
                connection.Dispose();
            }

            LogInfo("[ " + Name + " ] --INF-- Open order has no of additional users: " + listOfUsers.Count, logger);

            return listOfUsers;
        }

        private bool CheckOpenIdleForTypeFour(ILogger logger) {
            var openIdleIsTypeFour = false;
            var connection = new MySqlConnection(
                $"server={Program.IpAddress};port={Program.Port};userid={Program.Login};password={Program.Password};database={Program.Database};");
            try {
                connection.Open();
                var selectQuery = $"SELECT * FROM zapsi2.idle where OID in (SELECT IdleId from zapsi2.terminal_input_idle where DTE is NULL and DeviceId = {DeviceOid}) and IdleTypeID=4";
                var command = new MySqlCommand(selectQuery, connection);
                try {
                    var reader = command.ExecuteReader();
                    if (reader.Read()) {
                        openIdleIsTypeFour = true;
                    }

                    reader.Close();
                    reader.Dispose();
                } catch (Exception error) {
                    LogError("[ " + Name + " ] --ERR-- Problem checking idles for idletype=4: " + error.Message + selectQuery, logger);
                } finally {
                    command.Dispose();
                }

                connection.Close();
            } catch (Exception error) {
                LogError("[ " + Name + " ] --ERR-- Problem with database: " + error.Message, logger);
            } finally {
                connection.Dispose();
            }

            LogInfo("[ " + Name + " ] --INF-- Open idle is type four: " + openIdleIsTypeFour, logger);

            return openIdleIsTypeFour;
        }

        private static void LogInfo(string text, ILogger logger) {
            var now = DateTime.Now;
            text = now + " " + text;
            if (Program.OsIsLinux) {
                WriteLine(Program.greenColor + text + Program.resetColor);
            } else {
                logger.LogInformation(text);
                ForegroundColor = ConsoleColor.Green;
                WriteLine(text);
                ForegroundColor = ConsoleColor.White;
            }
        }


        private static void LogError(string text, ILogger logger) {
            var now = DateTime.Now;
            text = now + " " + text;
            if (Program.OsIsLinux) {
                WriteLine(Program.redColor + text + Program.resetColor);
            } else {
                logger.LogInformation(text);
                ForegroundColor = ConsoleColor.Red;
                WriteLine(text);
                ForegroundColor = ConsoleColor.White;
            }
        }

        public void UpdateOrderData(ILogger logger) {
            if (Program.AddCyclesToOrder.Equals("1")) {
                var connection =
                    new MySqlConnection(
                        $"server={Program.IpAddress};port={Program.Port};userid={Program.Login};password={Program.Password};database={Program.Database};");
                var count = GetCountForWorkplace(logger);
                var nokCount = GetNokCountForWorkplace(logger);
                var averageCycle = GetAverageCycleForWorkplace(count, logger);
                var averageCycleToInsert = averageCycle.ToString(CultureInfo.InvariantCulture).Replace(",", ".");
                var difference = DateTime.Now.Subtract(OrderStartDate).TotalSeconds
                    .ToString(CultureInfo.InvariantCulture);
                try {
                    connection.Open();
                    var command = connection.CreateCommand();

                    command.CommandText =
                        $"UPDATE `zapsi2`.`terminal_input_order` t SET t.Count={count}, t.Fail={nokCount}, t.AverageCycle={averageCycleToInsert}, t.Interval={difference} WHERE t.`DTE` is NULL and DeviceID={DeviceOid}";
                    LogInfo("[ " + Name + " ] --INF-- Update query: " + command.CommandText, logger);

                    try {
                        command.ExecuteNonQuery();
                    } catch (Exception error) {
                        LogError(
                            "[ " + Name + " ] --ERR-- problem updating count order: " + error.Message +
                            command.CommandText, logger);
                    } finally {
                        command.Dispose();
                    }

                    connection.Close();
                } catch (Exception error) {
                    LogError("[ " + Name + " ] --ERR-- Problem with database: " + error.Message, logger);
                } finally {
                    connection.Dispose();
                }
            }
        }

        private object GetNokCountForWorkplace(ILogger logger) {
            var nokCount = 0;
            var connection = new MySqlConnection(
                $"server={Program.IpAddress};port={Program.Port};userid={Program.Login};password={Program.Password};database={Program.Database};");
            var startDate = string.Format("{0:yyyy-MM-dd HH:mm:ss.ffff}", OrderStartDate);
            var actualDateTime = DateTime.Now;
            if (Program.TimezoneIsUtc) {
                actualDateTime = DateTime.UtcNow;
            }

            var endDate = string.Format("{0:yyyy-MM-dd HH:mm:ss.ffff}", actualDateTime);

            try {
                connection.Open();
                var selectQuery =
                    $"Select count(oid) as count from zapsi2.device_input_digital where DT>='{startDate}' and DT<='{endDate}' and DevicePortId={NokPortOid} and zapsi2.device_input_digital.Data=1";
                var command = new MySqlCommand(selectQuery, connection);
                try {
                    var reader = command.ExecuteReader();
                    while (reader.Read()) {
                        nokCount = Convert.ToInt32(reader["count"]);
                    }

                    reader.Close();
                    reader.Dispose();
                } catch (Exception error) {
                    LogError(
                        "[ " + Name + " ] --ERR-- Problem getting nok count for workplace: " + error.Message +
                        selectQuery, logger);
                } finally {
                    command.Dispose();
                }

                connection.Close();
            } catch (Exception error) {
                LogError("[ " + Name + " ] --ERR-- Problem with database: " + error.Message, logger);
            } finally {
                connection.Dispose();
            }

            return nokCount;
        }

        public void CreateIdleForWorkplace(ILogger logger, bool workplaceHasActiveOrder, DateTime dateTimeToInsert) {
            var myDate = string.Format("{0:yyyy-MM-dd HH:mm:ss}", dateTimeToInsert);
            var idleOidToInsert = 2;
            var userIdToInsert = "";
            if (workplaceHasActiveOrder) {
                idleOidToInsert = 1;
                userIdToInsert = OrderUserId.ToString();
            }

            if (userIdToInsert.Length == 0) {
                userIdToInsert = DownloadFromLoginTable(logger);
            }

            if (userIdToInsert.Equals("0") || userIdToInsert.Length == 0) {
                userIdToInsert = "NULL";
            }

            var connection =
                new MySqlConnection(
                    $"server={Program.IpAddress};port={Program.Port};userid={Program.Login};password={Program.Password};database={Program.Database};");
            try {
                connection.Open();
                var command = connection.CreateCommand();
                command.CommandText =
                    $"INSERT INTO `zapsi2`.`terminal_input_idle` (`DTS`, `DTE`, `IdleID`, `UserID`, `Interval`, `DeviceID`, `Note`) VALUES ('{myDate}', NULL , {idleOidToInsert}, {userIdToInsert}, 0, {DeviceOid}, 'Automatic idle')";

                try {
                    command.ExecuteNonQuery();
                } catch (Exception error) {
                    LogError(
                        "[ MAIN ] --ERR-- problem inserting idle into database: " + error.Message + command.CommandText,
                        logger);
                } finally {
                    command.Dispose();
                }

                connection.Close();
            } catch (Exception error) {
                LogError("[ " + Name + " ] --ERR-- Problem with database: " + error.Message, logger);
            } finally {
                connection.Dispose();
            }
        }

        private string DownloadFromLoginTable(ILogger logger) {
            var userIdFromLoginTable = "0";
            var connection = new MySqlConnection(
                $"server={Program.IpAddress};port={Program.Port};userid={Program.Login};password={Program.Password};database={Program.Database};");
            try {
                connection.Open();
                var selectQuery =
                    $"SELECT * from zapsi2.terminal_input_login where DTE is NULL and DeviceID={DeviceOid}";
                var command = new MySqlCommand(selectQuery, connection);
                try {
                    var reader = command.ExecuteReader();
                    if (reader.Read()) {
                        userIdFromLoginTable = Convert.ToString(reader["UserID"]);
                    }

                    reader.Close();
                    reader.Dispose();
                } catch (Exception error) {
                    LogError("[ " + Name + " ] --ERR-- Problem checking active order: " + error.Message + selectQuery,
                        logger);
                } finally {
                    command.Dispose();
                }

                connection.Close();
            } catch (Exception error) {
                LogError("[ " + Name + " ] --ERR-- Problem with database: " + error.Message, logger);
            } finally {
                connection.Dispose();
            }

            return userIdFromLoginTable;
        }

        public void CloseOrderForWorkplace(DateTime closingDateForOrder, bool closingOnPowerOff, ILogger logger) {
            var note = "";
            if (closingOnPowerOff) {
                note = "Poweroff closed";
            }

            var myDate = string.Format("{0:yyyy-MM-dd HH:mm:ss}", LastStateDateTime);
            var dateToInsert = string.Format("{0:yyyy-MM-dd HH:mm:ss}", closingDateForOrder);
            if (LastStateDateTime.CompareTo(closingDateForOrder) > 0) {
                dateToInsert = myDate;
            }

            var connection = new MySqlConnection(
                $"server={Program.IpAddress};port={Program.Port};userid={Program.Login};password={Program.Password};database={Program.Database};");
            var count = GetCountForWorkplace(logger);
            var nokCount = GetNokCountForWorkplace(logger);
            var averageCycle = GetAverageCycleForWorkplace(count, logger);
            var averageCycleToInsert = averageCycle.ToString(CultureInfo.InvariantCulture).Replace(",", ".");
            try {
                connection.Open();
                var command = connection.CreateCommand();
                command.CommandText =
                    $"UPDATE `zapsi2`.`terminal_input_order` t SET t.`DTE` = '{dateToInsert}', t.Interval = TIME_TO_SEC(timediff('{dateToInsert}', DTS)), t.`Count`={count}, t.Fail={nokCount}, t.averageCycle={averageCycleToInsert}, t.`Note`='{note}' WHERE t.`DTE` is NULL and DeviceID={DeviceOid};" +
                    $"UPDATE zapsi2.terminal_input_login t set t.DTE = '{dateToInsert}', t.Interval = TIME_TO_SEC(timediff('{dateToInsert}', DTS)) where t.DTE is null and t.DeviceId={DeviceOid};";
                try {
                    Console.WriteLine(command.CommandText);
                    command.ExecuteNonQuery();
                } catch (Exception error) {
                    LogError(
                        "[ MAIN ] --ERR-- problem closing order in database: " + error.Message + "\n" +
                        command.CommandText, logger);
                } finally {
                    command.Dispose();
                }

                OrderUserId = 0;
                connection.Close();
            } catch (Exception error) {
                LogError("[ " + Name + " ] --ERR-- Problem with database: " + error.Message, logger);
            } finally {
                connection.Dispose();
            }
        }

        private int GetCountForWorkplace(ILogger logger) {
            var count = 0;
            var connection = new MySqlConnection(
                $"server={Program.IpAddress};port={Program.Port};userid={Program.Login};password={Program.Password};database={Program.Database};");
            var startDate = string.Format("{0:yyyy-MM-dd HH:mm:ss.ffff}", OrderStartDate);
            var actualDateTime = DateTime.Now;
            if (Program.TimezoneIsUtc) {
                actualDateTime = DateTime.UtcNow;
            }

            var endDate = string.Format("{0:yyyy-MM-dd HH:mm:ss.ffff}", actualDateTime);

            try {
                connection.Open();
                var selectQuery =
                    $"Select count(oid) as count from zapsi2.device_input_digital where DT>='{startDate}' and DT<='{endDate}' and DevicePortId={CountPortOid} and zapsi2.device_input_digital.Data=1";
                var command = new MySqlCommand(selectQuery, connection);
                try {
                    var reader = command.ExecuteReader();
                    while (reader.Read()) {
                        count = Convert.ToInt32(reader["count"]);
                    }

                    reader.Close();
                    reader.Dispose();
                } catch (Exception error) {
                    LogError(
                        "[ " + Name + " ] --ERR-- Problem getting count for workplace: " + error.Message + selectQuery,
                        logger);
                } finally {
                    command.Dispose();
                }

                connection.Close();
            } catch (Exception error) {
                LogError("[ " + Name + " ] --ERR-- Problem with database: " + error.Message, logger);
            } finally {
                connection.Dispose();
            }

            return count;
        }

        private double GetAverageCycleForWorkplace(int count, ILogger logger) {
            var breakTimeInSeconds = GetBreakTime(logger);
            var averageCycle = 0.0;
            if (count != 0) {
                var difference = DateTime.Now.Subtract(OrderStartDate).TotalSeconds - breakTimeInSeconds;
                averageCycle = difference / count;
                if (averageCycle < 0) {
                    averageCycle = 0;
                }
            }

            return averageCycle;
        }

        private int GetBreakTime(ILogger logger) {
            var totalBreakTime = 0.0;
            var connection =
                new MySqlConnection(
                    $"server={Program.IpAddress};port={Program.Port};userid={Program.Login};password={Program.Password};database={Program.Database};");
            try {
                connection.Open();
                var selectQuery = $"SELECT * from zapsi2.break where WorkshiftID={ActualWorkshiftId}";
                LogInfo("[ " + Name + " ] --INF-- " + selectQuery, logger);
                var command = new MySqlCommand(selectQuery, connection);
                try {
                    var reader = command.ExecuteReader();
                    while (reader.Read()) {
                        var breakStart = Convert.ToString(reader["DTS"]);
                        var breakEnd = Convert.ToString(reader["DTE"]);
                        var breakOid = Convert.ToInt32(reader["OID"]);
                        var breakStartSplitted = breakStart.Split(':');
                        var breakEndSplitted = breakEnd.Split(':');
                        var breakStartDateTime = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day,
                            Convert.ToInt32(breakStartSplitted[0]), Convert.ToInt32(breakStartSplitted[1]), Convert.ToInt32(breakStartSplitted[2]));
                        var breakEndDateTime = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day,
                            Convert.ToInt32(breakEndSplitted[0]), Convert.ToInt32(breakEndSplitted[1]), Convert.ToInt32(breakEndSplitted[2]));
                        if (OrderStartDate < breakStartDateTime) {
                            LogInfo("[ " + Name + " ] --INF-- Break with oid " + breakOid + " is not yet active", logger);
                            if (breakStartDateTime < DateTime.Now && DateTime.Now < breakEndDateTime) {
                                LogInfo("[ " + Name + " ] --INF-- Break with oid " + breakOid + " is now active, calculating break elapsed time", logger);
                                totalBreakTime += (DateTime.Now - breakStartDateTime).TotalSeconds;
                            } else if (breakStartDateTime < DateTime.Now && breakEndDateTime < DateTime.Now) {
                                LogInfo("[ " + Name + " ] --INF-- Break with oid " + breakOid + " was already active, calculating all break time", logger);
                                totalBreakTime += (breakEndDateTime - breakStartDateTime).TotalSeconds;
                            }
                        }
                    }

                    reader.Close();
                    reader.Dispose();
                } catch (Exception error) {
                    LogError("[ " + Name + " ] --ERR-- Problem checking active order: " + error.Message + selectQuery,
                        logger);
                } finally {
                    command.Dispose();
                }

                connection.Close();
            } catch (Exception error) {
                LogError("[ " + Name + " ] --ERR-- Problem with database: " + error.Message, logger);
            } finally {
                connection.Dispose();
            }

            return (int) totalBreakTime;
        }

        public void CreateOrderForWorkplace(ILogger logger) {
            var workplaceModeId = GetWorkplaceModeId(logger);
            var myDate = string.Format("{0:yyyy-MM-dd HH:mm:ss}", DateTime.Now);
            var connection = new MySqlConnection(
                $"server={Program.IpAddress};port={Program.Port};userid={Program.Login};password={Program.Password};database={Program.Database};");
            try {
                connection.Open();
                var command = connection.CreateCommand();
                command.CommandText =
                    $"INSERT INTO `zapsi2`.`terminal_input_order` (`DTS`, `DTE`, `OrderID`, `UserID`, `DeviceID`, `Interval`, `Count`, `Fail`, `AverageCycle`, `WorkerCount`, `WorkplaceModeID`, `Note`, `WorkshiftID`) VALUES ('{myDate}', NULL, {DefaultOrder}, NULL, {DeviceOid}, 0, DEFAULT, DEFAULT, DEFAULT, DEFAULT, {workplaceModeId}, 'NULL', {ActualWorkshiftId})";
                try {
                    command.ExecuteNonQuery();
                } catch (Exception error) {
                    LogError(
                        "[ MAIN ] --ERR-- problem inserting terminal input order into database: " + error.Message +
                        command.CommandText, logger);
                } finally {
                    command.Dispose();
                }

                connection.Close();
            } catch (Exception error) {
                LogError("[ " + Name + " ] --ERR-- Problem with database: " + error.Message, logger);
            } finally {
                connection.Dispose();
            }
        }

        private int GetWorkplaceModeId(ILogger logger) {
            var workplaceModeId = 1;
            var connection = new MySqlConnection(
                $"server={Program.IpAddress};port={Program.Port};userid={Program.Login};password={Program.Password};database={Program.Database};");
            try {
                connection.Open();
                var selectQuery = $"SELECT * from zapsi2.workplace_mode where WorkplaceId={Oid}";
                var command = new MySqlCommand(selectQuery, connection);
                try {
                    var reader = command.ExecuteReader();
                    if (reader.Read()) {
                        workplaceModeId = Convert.ToInt32(reader["OID"]);
                    }

                    reader.Close();
                    reader.Dispose();
                } catch (Exception error) {
                    LogError("[ " + Name + " ] --ERR-- Problem checking active order: " + error.Message + selectQuery,
                        logger);
                } finally {
                    command.Dispose();
                }

                connection.Close();
            } catch (Exception error) {
                LogError("[ " + Name + " ] --ERR-- Problem with database: " + error.Message, logger);
            } finally {
                connection.Dispose();
            }

            return workplaceModeId;
        }

        public bool CheckIfWorkplaceHasActivedIdle(ILogger logger) {
            var workplaceHasActiveIdle = false;
            var connection = new MySqlConnection(
                $"server={Program.IpAddress};port={Program.Port};userid={Program.Login};password={Program.Password};database={Program.Database};");
            try {
                connection.Open();
                var selectQuery =
                    $"SELECT * from zapsi2.terminal_input_idle where DTE is null and  DeviceID={DeviceOid}";
                var command = new MySqlCommand(selectQuery, connection);
                try {
                    var reader = command.ExecuteReader();
                    if (reader.Read()) {
                        WorkplaceIdleId = Convert.ToInt32(reader["IdleID"]);
                        workplaceHasActiveIdle = true;
                    }

                    reader.Close();
                    reader.Dispose();
                } catch (Exception error) {
                    LogError("[ " + Name + " ] --ERR-- Problem checking active idle: " + error.Message + selectQuery,
                        logger);
                } finally {
                    command.Dispose();
                }

                connection.Close();
            } catch (Exception error) {
                LogError("[ " + Name + " ] --ERR-- Problem with database: " + error.Message, logger);
            } finally {
                connection.Dispose();
            }

            return workplaceHasActiveIdle;
        }

        public bool CheckIfWorkplaceHasActiveOrder(ILogger logger) {
            var workplaceHasActiveOrder = false;
            var connection = new MySqlConnection(
                $"server={Program.IpAddress};port={Program.Port};userid={Program.Login};password={Program.Password};database={Program.Database};");
            try {
                connection.Open();
                var selectQuery =
                    $"SELECT * from zapsi2.terminal_input_order where DTE is NULL and DeviceID={DeviceOid}";
                var command = new MySqlCommand(selectQuery, connection);
                try {
                    var reader = command.ExecuteReader();
                    if (reader.Read()) {
                        workplaceHasActiveOrder = true;
                        OrderStartDate = Convert.ToDateTime(reader["DTS"]);
                        try {
                            OrderUserId = Convert.ToInt32(reader["UserID"]);
                        } catch (Exception) {
                            OrderUserId = null;
                            LogInfo("[ " + Name + " ] --INF-- Open order has no user", logger);
                        }
                    } else {
                        OrderUserId = null;
                    }

                    reader.Close();
                    reader.Dispose();
                } catch (Exception error) {
                    LogError("[ " + Name + " ] --ERR-- Problem checking active order: " + error.Message + selectQuery,
                        logger);
                } finally {
                    command.Dispose();
                }

                connection.Close();
            } catch (Exception error) {
                LogError("[ " + Name + " ] --ERR-- Problem with database: " + error.Message, logger);
            } finally {
                connection.Dispose();
            }

            LogInfo("[ " + Name + " ] --INF-- Workplace has open order: " + workplaceHasActiveOrder, logger);

            return workplaceHasActiveOrder;
        }

        public void UpdateActualStateForWorkplace(ILogger logger) {
            var connection = new MySqlConnection(
                $"server={Program.IpAddress};port={Program.Port};userid={Program.Login};password={Program.Password};database={Program.Database};");
            try {
                var stateNumber = 1;
                connection.Open();
                var selectQuery = $"SELECT * from zapsi2.workplace_state where DTE is NULL and WorkplaceID={Oid}";
                var command = new MySqlCommand(selectQuery, connection);
                try {
                    var reader = command.ExecuteReader();
                    if (reader.Read()) {
                        LastStateDateTime = Convert.ToDateTime(reader["DTS"].ToString());
                        stateNumber = Convert.ToInt32(reader["StateID"]);
                    }

                    reader.Close();
                    reader.Dispose();
                } catch (Exception error) {
                    LogError(
                        "[ " + Name + " ] --ERR-- Problem getting actual workplace state: " + error.Message +
                        selectQuery, logger);
                }

                selectQuery = $"SELECT * from zapsi2.state where OID={stateNumber}";
                command = new MySqlCommand(selectQuery, connection);
                try {
                    var reader = command.ExecuteReader();
                    if (reader.Read()) {
                        var stateType = Convert.ToString(reader["Type"]);
                        if (stateType.Equals("running")) {
                            ActualStateType = StateType.Running;
                        } else if (stateType.Equals("idle")) {
                            ActualStateType = StateType.Idle;
                        } else {
                            ActualStateType = StateType.PowerOff;
                        }
                    }

                    reader.Close();
                    reader.Dispose();
                } catch (Exception error) {
                    LogError(
                        "[ " + Name + " ] --ERR-- Problem getting actual workplace state: " + error.Message +
                        selectQuery, logger);
                } finally {
                    command.Dispose();
                }


                connection.Close();
            } catch (Exception error) {
                LogError("[ " + Name + " ] --ERR-- Problem with database: " + error.Message, logger);
            } finally {
                connection.Dispose();
            }
        }

        public int GetActualWorkShiftIdFor(ILogger logger) {
            PreviousWorkshiftId = ActualWorkshiftId;

            var actualWorkShiftId = 0;
            var connection = new MySqlConnection(
                $"server={Program.IpAddress};port={Program.Port};userid={Program.Login};password={Program.Password};database={Program.Database};");
            var actualTime = DateTime.Now;
            if (Program.TimezoneIsUtc) {
                actualTime = DateTime.UtcNow;
            }

            try {
                connection.Open();
                var selectQuery =
                    $"SELECT * from zapsi2.workshift where Active=1 and WorkplaceDivisionID is null or WorkplaceDivisionID ={WorkplaceDivisionId}";
                var command = new MySqlCommand(selectQuery, connection);
                try {
                    var reader = command.ExecuteReader();
                    while (reader.Read()) {
                        var oid = Convert.ToInt32(reader["OID"]);
                        var shiftStartsAt = reader["WorkshiftStart"].ToString();
                        var shiftDuration = Convert.ToInt32(reader["WorkshiftLenght"]);
                        var shiftStartAtDateTime =
                            DateTime.ParseExact(shiftStartsAt, "HH:mm:ss", CultureInfo.CurrentCulture);
                        if (Program.TimezoneIsUtc) {
                            shiftStartAtDateTime = DateTime
                                .ParseExact(shiftStartsAt, "HH:mm:ss", CultureInfo.CurrentCulture)
                                .ToUniversalTime();
                        }

                        var shiftEndsAtDateTime = shiftStartAtDateTime.AddMinutes(shiftDuration);
                        if (actualTime.Ticks < shiftStartAtDateTime.Ticks) {
                            actualTime = actualTime.AddDays(1);
                        }

                        if (actualTime.Ticks >= shiftStartAtDateTime.Ticks &&
                            actualTime.Ticks < shiftEndsAtDateTime.Ticks) {
                            actualWorkShiftId = oid;
                            LogInfo("[ " + Name + " ] --INF-- Actual workshift id: " + actualWorkShiftId, logger);
                        }
                    }

                    reader.Close();
                    reader.Dispose();
                } catch (Exception error) {
                    LogError(
                        "[ " + Name + " ] --ERR-- Problem getting actual workshift: " + error.Message + selectQuery,
                        logger);
                } finally {
                    command.Dispose();
                }

                connection.Close();
            } catch (Exception error) {
                LogError("[ " + Name + " ] --ERR-- Problem with database: " + error.Message, logger);
            } finally {
                connection.Dispose();
            }

            CheckForFirstRunWhenPreviouslyWasNoShift(actualWorkShiftId);
            return actualWorkShiftId;
        }

        private void CheckForFirstRunWhenPreviouslyWasNoShift(int actualWorkShiftId) {
            if (PreviousWorkshiftId == 0) {
                PreviousWorkshiftId = actualWorkShiftId;
            }
        }

        public void AddCountPort(ILogger logger) {
            var connection = new MySqlConnection(
                $"server={Program.IpAddress};port={Program.Port};userid={Program.Login};password={Program.Password};database={Program.Database};");
            try {
                connection.Open();

                var selectQuery =
                    $"select * from zapsi2.workplace_port where WorkplaceID = {Oid} and Type in ('cycle','running') order by Type asc limit 1;";
                var command = new MySqlCommand(selectQuery, connection);
                try {
                    var reader = command.ExecuteReader();
                    while (reader.Read()) {
                        CountPortOid = Convert.ToInt32(reader["DevicePortID"]);
                    }

                    reader.Close();
                    reader.Dispose();
                } catch (Exception error) {
                    LogError("[ " + Name + " ] --ERR-- Problem reading from database: " + error.Message + selectQuery,
                        logger);
                } finally {
                    command.Dispose();
                }

                connection.Close();
            } catch (Exception error) {
                LogError("[ " + Name + " ] --ERR-- Problem with database: " + error.Message, logger);
            } finally {
                connection.Dispose();
            }
        }

        public void AddProductionPort(ILogger logger) {
            var connection = new MySqlConnection(
                $"server={Program.IpAddress};port={Program.Port};userid={Program.Login};password={Program.Password};database={Program.Database};");
            try {
                connection.Open();

                var selectQuery =
                    $"select * from zapsi2.workplace_port where WorkplaceID = {Oid} and Type in ('cycle','running') order by Type desc limit 1;";
                var command = new MySqlCommand(selectQuery, connection);
                try {
                    var reader = command.ExecuteReader();
                    while (reader.Read()) {
                        ProductionPortOid = Convert.ToInt32(reader["DevicePortID"]);
                    }

                    reader.Close();
                    reader.Dispose();
                } catch (Exception error) {
                    LogError("[ " + Name + " ] --ERR-- Problem adding production port: " + error.Message + selectQuery,
                        logger);
                } finally {
                    command.Dispose();
                }

                connection.Close();
            } catch (Exception error) {
                LogError("[ " + Name + " ] --ERR-- Problem with database: " + error.Message, logger);
            } finally {
                connection.Dispose();
            }
        }

        public void CreateDefaultOrder(ILogger logger) {
            var connection = new MySqlConnection(
                $"server={Program.IpAddress};port={Program.Port};userid={Program.Login};password={Program.Password};database={Program.Database};");
            try {
                connection.Open();
                var command = connection.CreateCommand();

                command.CommandText =
                    $"INSERT INTO `zapsi2`.`order` (`Name`, `Barcode`, `ProductID`, `OrderStatusID`, `CountRequested`, `WorkplaceID`) VALUES ('Internal', '1', {DefaultProduct}, 1, 0, {Oid})";
                try {
                    command.ExecuteNonQuery();
                } catch (Exception error) {
                    LogError(
                        "[ MAIN ] --ERR-- problem inserting default order into database: " + error.Message +
                        command.CommandText, logger);
                } finally {
                    command.Dispose();
                }

                connection.Close();
            } catch (Exception error) {
                LogError("[ " + Name + " ] --ERR-- Problem with database: " + error.Message, logger);
            } finally {
                connection.Dispose();
            }
        }

        public bool CheckForDefaultOrder(ILogger logger) {
            var defaultOrderIsInDatabase = false;
            var connection = new MySqlConnection(
                $"server={Program.IpAddress};port={Program.Port};userid={Program.Login};password={Program.Password};database={Program.Database};");
            try {
                connection.Open();
                const string selectQuery = @"SELECT * from zapsi2.order where Name like 'Internal' limit 1";
                var command = new MySqlCommand(selectQuery, connection);
                try {
                    var reader = command.ExecuteReader();
                    while (reader.Read()) {
                        DefaultOrder = Convert.ToInt32(reader["OID"]);
                        defaultOrderIsInDatabase = true;
                    }

                    reader.Close();
                    reader.Dispose();
                } catch (Exception error) {
                    LogError("[ MAIN ] --ERR-- Problem checking default order: " + error.Message + selectQuery, logger);
                } finally {
                    command.Dispose();
                }

                connection.Close();
            } catch (Exception error) {
                LogError("[ " + Name + " ] --ERR-- Problem with database: " + error.Message, logger);
            } finally {
                connection.Dispose();
            }

            return defaultOrderIsInDatabase;
        }


        public bool CheckForDefaultProduct(ILogger logger) {
            var defaultProductIsInDatabase = false;
            var connection = new MySqlConnection(
                $"server={Program.IpAddress};port={Program.Port};userid={Program.Login};password={Program.Password};database={Program.Database};");
            try {
                connection.Open();
                const string selectQuery = @"SELECT * from zapsi2.product where Name like 'Internal' limit 1";
                var command = new MySqlCommand(selectQuery, connection);
                try {
                    var reader = command.ExecuteReader();
                    while (reader.Read()) {
                        DefaultProduct = Convert.ToInt32(reader["OID"]);
                        defaultProductIsInDatabase = true;
                    }

                    reader.Close();
                    reader.Dispose();
                } catch (Exception error) {
                    LogError("[ MAIN ] --ERR-- Problem checking default product: " + error.Message + selectQuery,
                        logger);
                } finally {
                    command.Dispose();
                }

                connection.Close();
            } catch (Exception error) {
                LogError("[ " + Name + " ] --ERR-- Problem with database: " + error.Message, logger);
            } finally {
                connection.Dispose();
            }

            return defaultProductIsInDatabase;
        }

        public void CreateDefaultProduct(ILogger logger) {
            var connection = new MySqlConnection(
                $"server={Program.IpAddress};port={Program.Port};userid={Program.Login};password={Program.Password};database={Program.Database};");
            try {
                connection.Open();
                var command = connection.CreateCommand();

                command.CommandText =
                    ("INSERT INTO `zapsi2`.`product` (`Name`, `Barcode`, `Cycle`, `IdleFromTime`, `ProductStatusID`, `Deleted`, `ProductGroupID`) VALUES ('Internal', '1', 1, 10, 1, 0, NULL)"
                    );
                try {
                    command.ExecuteNonQuery();
                } catch (Exception error) {
                    LogError(
                        "[ MAIN ] --ERR-- problem inserting default product into database: " + error.Message +
                        command.CommandText, logger);
                } finally {
                    command.Dispose();
                }

                connection.Close();
            } catch (Exception error) {
                LogError("[ MAIN ] --ERR-- Problem with database: " + error.Message, logger);
            } finally {
                connection.Dispose();
            }
        }

        public bool CheckForOneFromLastOrderDte(ILogger logger) {
            var productionIsOne = false;
            var connection = new MySqlConnection(
                $"server={Program.IpAddress};port={Program.Port};userid={Program.Login};password={Program.Password};database={Program.Database};");
            try {
                connection.Open();
                var selectQuery =
                    $"Select * from zapsi2.device_input_digital where DevicePortID={ProductionPortOid} and DT > (select DTE from terminal_input_order where DeviceID = {DeviceOid} order by DTS desc limit 1) order by DT desc limit 2";
                var command = new MySqlCommand(selectQuery, connection);
                try {
                    var reader = command.ExecuteReader();
                    while (reader.Read()) {
                        var data = Convert.ToInt32(reader["Data"]);
                        var lastDataDateTime = Convert.ToDateTime(reader["DT"]);
                        if (Program.TimezoneIsUtc) {
                            if (data == 1 && (DateTime.UtcNow - lastDataDateTime).TotalSeconds < 10) {
                                productionIsOne = true;
                            }
                        } else {
                            if (data == 1 && (DateTime.Now - lastDataDateTime).TotalSeconds < 10) {
                                productionIsOne = true;
                            }
                        }
                    }

                    reader.Close();
                    reader.Dispose();
                } catch (Exception error) {
                    LogError(
                        "[ " + Name + " ] --ERR-- Problem getting count for workplace: " + error.Message + selectQuery,
                        logger);
                } finally {
                    command.Dispose();
                }

                connection.Close();
            } catch (Exception error) {
                LogError("[ " + Name + " ] --ERR-- Problem with database: " + error.Message, logger);
            } finally {
                connection.Dispose();
            }

            return productionIsOne;
        }

        public void AddFailPort(ILogger logger) {
            var connection = new MySqlConnection(
                $"server={Program.IpAddress};port={Program.Port};userid={Program.Login};password={Program.Password};database={Program.Database};");
            try {
                connection.Open();

                var selectQuery =
                    $"select * from zapsi2.workplace_port where WorkplaceID = {Oid} and Type in ('fail') order by Type asc limit 1;";
                var command = new MySqlCommand(selectQuery, connection);
                try {
                    var reader = command.ExecuteReader();
                    while (reader.Read()) {
                        NokPortOid = Convert.ToInt32(reader["DevicePortID"]);
                    }

                    reader.Close();
                    reader.Dispose();
                } catch (Exception error) {
                    LogError("[ " + Name + " ] --ERR-- Problem reading from database: " + error.Message + selectQuery,
                        logger);
                } finally {
                    command.Dispose();
                }

                connection.Close();
            } catch (Exception error) {
                LogError("[ " + Name + " ] --ERR-- Problem with database: " + error.Message, logger);
            } finally {
                connection.Dispose();
            }
        }


        private int GetCavityForOrder(ILogger logger) {
            var cavity = 1;
            var connection = new MySqlConnection(
                $"server={Program.IpAddress};port={Program.Port};userid={Program.Login};password={Program.Password};database={Program.Database};");
            try {
                connection.Open();
                var selectQuery =
                    $"SELECT * from zapsi2.terminal_input_order where DeviceID={DeviceOid} and DTE is null";
                var command = new MySqlCommand(selectQuery, connection);
                try {
                    var reader = command.ExecuteReader();
                    if (reader.Read()) {
                        cavity = Convert.ToInt32(reader["Cavity"]);
                    }

                    reader.Close();
                    reader.Dispose();
                } catch (Exception error) {
                    LogError("[ " + Name + " ] --ERR-- Problem checking active order: " + error.Message + selectQuery,
                        logger);
                } finally {
                    command.Dispose();
                }

                connection.Close();
            } catch (Exception error) {
                LogError("[ " + Name + " ] --ERR-- Problem with database: " + error.Message, logger);
            } finally {
                connection.Dispose();
            }

            return cavity;
        }

        private int GetIdleId(ILogger logger) {
            var actualIdleId = 0;
            var connection = new MySqlConnection(
                $"server={Program.IpAddress};port={Program.Port};userid={Program.Login};password={Program.Password};database={Program.Database};");
            try {
                connection.Open();
                var selectQuery =
                    $"SELECT * from zapsi2.terminal_input_idle where DeviceID={DeviceOid} and DTE is null";
                var command = new MySqlCommand(selectQuery, connection);
                try {
                    var reader = command.ExecuteReader();
                    if (reader.Read()) {
                        actualIdleId = Convert.ToInt32(reader["IdleID"]);
                    }

                    reader.Close();
                    reader.Dispose();
                } catch (Exception error) {
                    LogError("[ " + Name + " ] --ERR-- Problem checking active idle: " + error.Message + selectQuery,
                        logger);
                } finally {
                    command.Dispose();
                }

                connection.Close();
            } catch (Exception error) {
                LogError("[ " + Name + " ] --ERR-- Problem with database: " + error.Message, logger);
            } finally {
                connection.Dispose();
            }

            return actualIdleId;
        }

        public void UpdateOrderIdleTable(ILogger logger) {
            var connection =
                new MySqlConnection(
                    $"server={Program.IpAddress};port={Program.Port};userid={Program.Login};password={Program.Password};database={Program.Database};");
            try {
                connection.Open();
                var terminalInputOrder = 1;
                var orderQuery =
                    $"SELECT * from zapsi2.terminal_input_order where DTE is NULL and DeviceID={DeviceOid}";
                var orderCommand = new MySqlCommand(orderQuery, connection);
                try {
                    var reader = orderCommand.ExecuteReader();
                    if (reader.Read()) {
                        terminalInputOrder = Convert.ToInt32(reader["OID"]);
                    }

                    reader.Close();
                    reader.Dispose();
                } catch (Exception error) {
                    LogError("[ " + Name + " ] --ERR-- Problem checking active order: " + error.Message + orderQuery,
                        logger);
                } finally {
                    orderCommand.Dispose();
                }

                var terminalInputIdle = 1;
                var idleQuery = $"SELECT * from zapsi2.terminal_input_idle where DTE is NULL and DeviceID={DeviceOid}";
                var idleCommand = new MySqlCommand(idleQuery, connection);
                try {
                    var reader = idleCommand.ExecuteReader();
                    if (reader.Read()) {
                        terminalInputIdle = Convert.ToInt32(reader["OID"]);
                    }

                    reader.Close();
                    reader.Dispose();
                } catch (Exception error) {
                    LogError("[ " + Name + " ] --ERR-- Problem checking active idle: " + error.Message + orderQuery,
                        logger);
                } finally {
                    idleCommand.Dispose();
                }

                var insertCommand = connection.CreateCommand();
                insertCommand.CommandText =
                    $"INSERT INTO `zapsi2`.`terminal_input_order_terminal_input_idle` (`TerminalInputOrderID`, `TerminalInputIdleID`) VALUES ({terminalInputOrder}, {terminalInputIdle})";

                try {
                    insertCommand.ExecuteNonQuery();
                } catch (Exception error) {
                    LogError(
                        "[ MAIN ] --ERR-- problem inserting terminal_input_order_idle into database: " + error.Message +
                        orderCommand.CommandText, logger);
                } finally {
                    insertCommand.Dispose();
                }

                connection.Close();
            } catch (Exception error) {
                LogError("[ " + Name + " ] --ERR-- Problem with database: " + error.Message, logger);
            } finally {
                connection.Dispose();
            }
        }

        public void UpdateWorkplaceIdleTime(ILogger logger) {
            var connection =
                new MySqlConnection(
                    $"server={Program.IpAddress};port={Program.Port};userid={Program.Login};password={Program.Password};database={Program.Database};");
            try {
                connection.Open();
                var idleFromTime = 1;
                var orderQuery =
                    $"SELECT * from zapsi2.workplace_mode where workplace_mode.WorkplaceModeTypeID=1 and WorkplaceId={Oid}";
                var orderCommand = new MySqlCommand(orderQuery, connection);
                try {
                    var reader = orderCommand.ExecuteReader();
                    if (reader.Read()) {
                        idleFromTime = Convert.ToInt32(reader["IdleFromTime"]);
                    }

                    reader.Close();
                    reader.Dispose();
                } catch (Exception error) {
                    LogError("[ " + Name + " ] --ERR-- Problem checking workplace_mode: " + error.Message + orderQuery,
                        logger);
                } finally {
                    orderCommand.Dispose();
                }

                var insertCommand = connection.CreateCommand();
                insertCommand.CommandText = $"UPDATE zapsi2.workplace SET IdleFromTime={idleFromTime} WHERE OID={Oid}";

                try {
                    insertCommand.ExecuteNonQuery();
                } catch (Exception error) {
                    LogError(
                        "[ MAIN ] --ERR-- problem updating workplace idle time: " + error.Message +
                        orderCommand.CommandText, logger);
                } finally {
                    insertCommand.Dispose();
                }

                connection.Close();
            } catch (Exception error) {
                LogError("[ " + Name + " ] --ERR-- Problem with database: " + error.Message, logger);
            } finally {
                connection.Dispose();
            }
        }
    }
}