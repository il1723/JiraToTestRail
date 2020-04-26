using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HtmlAgilityPack;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;

namespace JiraToTestRail
{
    class Program
    {
        static void Main(string[] args)
        {
            //some output to a user and reading Jira URL
            Console.WriteLine("Hello, this tool will help you migrating data from Jira ticket to TestRail Case");
            Console.WriteLine("Please enter Jira ticket URL:");
            var url =  Console.ReadLine();

            //check if url is correct
            if (!checkURL(url)) { 
                Console.WriteLine("URL \""+url+"\" is invalid"); 
            }


            //this is a small hack here to get current user profile and avoid automating login to Jira and TestRail. this requires Chrome browser closed.
            ChromeOptions options = new ChromeOptions();
            options.AddArguments(@"--user-data-dir=C:\Users\44775\AppData\Local\Google\Chrome\User Data");
            options.AddArguments(@"--log-level=OFF");


            //some vars initialisation
            var jiraID = String.Empty;
            var title = String.Empty;
            var description = url;
            var businessGoal = new List<parseTable>();
            var preconditions = new List<parseTable>();
            var scenario = new List<parseTable>();


            //open browser
            using (ChromeDriver driver = new ChromeDriver(options))
            {

                //go to Jira url
                driver.Url = url;
                driver.Navigate();
                

                //parsing Jira page
                HtmlDocument htmlDoc = new HtmlDocument();
                htmlDoc.LoadHtml(driver.PageSource);
                if (htmlDoc.DocumentNode != null)
                {
                    var bodyNode = htmlDoc.DocumentNode.SelectSingleNode("//body");

                    if (bodyNode != null && bodyNode.SelectNodes("//*[contains(@data-test-id,'issue.views.issue-base.foundation.summary.heading')]") != null)
                    {
                        //scrape all required data points into vars. that requires Jira ticket to be formated in certain way as per template
                        jiraID = bodyNode.SelectNodes("//*[contains(@data-test-id,'issue.views.issue-base.foundation.breadcrumbs.breadcrumb-current-issue-container')]").First().InnerText.Trim().Replace("Copy the link to this issue", "");
                        title = bodyNode.SelectNodes("//*[contains(@data-test-id,'issue.views.issue-base.foundation.summary.heading')]").First().InnerText.Trim();
                        var content = bodyNode.SelectNodes("//*[contains(@role,'presentation')]").First(a => a.Name == "div");
                        businessGoal = findContent(content, "Business Goal", true, true);
                        preconditions = findContent(content, "Pre-conditions", false, true);
                        scenario = findContent(content, "Scenario", false, false);
                    }
                }
                else
                {
                    Console.WriteLine("Page not found or invalid HTML");
                }


                //quick glance if scraped data is there and ask for TestRail url
                Console.WriteLine();
                Console.WriteLine();
                Console.WriteLine();
                Console.WriteLine("Please verify scraped data:");
                Console.WriteLine("Jira ticket title: " + title);
                Console.WriteLine("Business goal: " + listToText(businessGoal));

                Console.WriteLine();
                Console.WriteLine("Please enter TestRail url if correct and X if not:");

                url = Console.ReadLine();
                if (!checkURL(url))
                {
                    Console.WriteLine("URL \"" + url + "\" is invalid");
                }
                if (url == "X") {
                    return;
                }


                //navigate to TestRail url
                driver.Url = url;
                driver.Navigate();
                htmlDoc.LoadHtml(driver.PageSource);
                if (htmlDoc.DocumentNode != null)
                {
                    var bodyNode = htmlDoc.DocumentNode.SelectSingleNode("//body");

                    //click on create Section button
                    if (driver.FindElement(By.Id("addSection")).Displayed)
                    {
                        driver.FindElement(By.Id("addSection")).Click();
                    }
                    else
                    {
                        driver.FindElement(By.Id("addSectionInline")).Click();
                    }
                    Thread.Sleep(1000);

                    //fill form details and save
                    driver.FindElement(By.Id("editSectionName")).SendKeys(jiraID + ": " + title);
                    driver.FindElement(By.Id("editSectionDescription")).SendKeys(description);
                    driver.FindElement(By.Id("editSectionSubmit")).Click();
                    Thread.Sleep(1000);

                    //click on addCase
                    driver.FindElement(By.Id("addCase")).Click();
                    Thread.Sleep(1000);
                    
                    
                    //below fill Case details, very annoyingly SendKeys auto-submits the form, so had to go javascript route of filling form controls

                    //driver.FindElement(By.Id("title")).SendKeys(text);
                    driver.ExecuteScript("arguments[0].value = arguments[1]", driver.FindElement(By.Id("title")), listToText(businessGoal));

                    //driver.FindElement(By.Id("custom_preconds")).SendKeys(text);
                    driver.ExecuteScript("arguments[0].value = arguments[1]", driver.FindElement(By.Id("custom_preconds")), listToText(preconditions));
                    
                    // driver.FindElement(By.Id("custom_steps")).SendKeys(text);
                    driver.ExecuteScript("arguments[0].value = arguments[1]", driver.FindElement(By.Id("custom_steps")), listToText(scenario));

                    //driver.FindElement(By.Id("custom_expected")).SendKeys(text);
                    driver.ExecuteScript("arguments[0].value = arguments[1]", driver.FindElement(By.Id("custom_expected")), listToText(businessGoal));


                }


                Console.WriteLine();
                Console.WriteLine();
                Console.WriteLine();
                Console.WriteLine("Please edit the Case and save before pressing Enter button here!!!");
                
                url = Console.ReadLine();
                
            }


        }

        private static bool checkURL(string url) {
            //todo verify valid url
            return true;
        }

        //parse Jira ticket description, basically scrape a table going after required Title into a normalized view
        private static List<parseTable> findContent(HtmlNode content, string header, bool takeHeader, bool takeStartColumn) {
            var returnText = new List<parseTable>();
            var nextTable = false;
            foreach (HtmlNode item in content.Descendants())
            {
                if (item.ChildNodes.Count == 0 && item.InnerText == header)
                {
                    nextTable = true;
                }
                if (nextTable && item.Name == "table")
                {
                    var trCount = 0; 
                    foreach (HtmlNode trItem in item.Descendants().Where(a => a.Name == "tr"))
                    {
                        var tdCount = 0;
                        if (trCount > 0 || takeHeader)
                        {
                            var parseColumns = new parseTable();
                            foreach (HtmlNode tdItem in trItem.Descendants().Where(a => a.Name == "td"))
                            {
                                if (tdCount > 0 || takeStartColumn)
                                {
                                    if (parseColumns.value1 == String.Empty)
                                    {
                                        if (tdItem.InnerText.Trim() != String.Empty)
                                        {
                                            parseColumns.value1 = tdItem.InnerText.Trim();
                                        }
                                        else {
                                            parseColumns.value1 = "";
                                        }
                                    }
                                    else {
                                        if (parseColumns.value2 == String.Empty)
                                        {
                                            parseColumns.value2 = tdItem.InnerText.Trim();
                                        }
                                    }
                                }
                                
                                tdCount++;
                            }
                            returnText.Add(parseColumns);
                        }
                        trCount++;
                    }
                    break;
                }
            }
            return returnText;
        }

        //parseTable data to a string
        private static string listToText(List<parseTable> entity) {
            var text = String.Empty;
            foreach (var item in entity)
            {
                text += item.value1 + " " + item.value2 + "\r\n";
            }
            return text;
        }

        //very silly class to store a pair of columns from a table
        public class parseTable
        {
            public string value1 { get; set; }
            public string value2 { get; set; }

            public parseTable()
            {
                this.value1 = String.Empty;
                this.value2 = String.Empty;
            }
        }
    }
}
