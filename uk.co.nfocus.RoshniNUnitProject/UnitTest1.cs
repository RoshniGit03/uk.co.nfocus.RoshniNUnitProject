using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using NUnit.Framework;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI; //WebDriverWait functionality
using System;
using System.Globalization; //culture-specific formatting

namespace uk.co.nfocus.RoshniNUnitProject
{
    public class DiscountTests
    {
        private IWebDriver _driver; //private variable to hold the WebDriver instance
        private string _baseUrl = "https://www.edgewordstraining.co.uk/demo-site";

        //setup method to initialise ChromeDriver and navigate to base URL
        [SetUp]
        public void Setup()
        {
            _driver = new ChromeDriver();
            _driver.Manage().Window.Maximize(); //maximize browser window
            _driver.Navigate().GoToUrl($"{_baseUrl}/my-account/"); //navigate to login page
        }

        //Test method to validate discount application during purchase
        [Test]
        public void TestDiscountPurchase()
        {
            //initialize page objects
            var loginPage = new LoginPage(_driver); //create instance of LoginPage
            var shopPage = new ShopPage(_driver);
            var cartPage = new CartPage(_driver); 
            var accountPage = new AccountPage(_driver);

            //perform login using registered email and password (I have manually registered this beforehand)
            loginPage.Login("email@address.com", "strong!password");

            //navigate to shop page to select an item to purchase
            _driver.FindElement(By.LinkText("Shop")).Click();

            //add specific item (polo shirt) to the cart
            shopPage.AddPoloToCart();

            //view the cart to proceed to checkout steps
            _driver.FindElement(By.CssSelector("a[href*='cart']")).Click();

            //apply coupon code for discount and retrieve cart totals
            cartPage.ApplyCoupon("edgewords");
            var (subtotal, discount, shipping, total) = cartPage.GetCartTotals();

            //calculate the expected discount as 15% of subtotal and verify
            decimal expectedDiscount = subtotal * 0.15m; //I changed this percentage about to check if it displays the error and it does
            Assert.That(discount, Is.EqualTo(expectedDiscount).Within(0.01m), "Coupon discount is not 15% of the subtotal.");
            //used Within(0.01m) to allow for minor floating-point precision errors in financial calculations

            //calculate expected total after discount and shipping, verify it
            decimal expectedTotal = subtotal - discount + shipping; 
            Assert.That(total, Is.EqualTo(expectedTotal).Within(0.01m), "Total amount after discount and shipping is incorrect."); //verify total matches expected

            //log success message if both assertions pass
            Console.WriteLine("Test passed: Coupon removes 15% and total amount matches the expected total after discount and shipping.");

            //logout from account to complete test case
            accountPage.Logout();
        }

        //Test method to validate order number capture after purchasing an item
        [Test]
        public void TestOrderNumber()
        {
            //initialise page objects, and complete prior steps as in Test 1

            var loginPage = new LoginPage(_driver);
            var shopPage = new ShopPage(_driver);
            var cartPage = new CartPage(_driver);
            var checkoutPage = new CheckoutPage(_driver);
            var accountPage = new AccountPage(_driver);

            //login
            loginPage.Login("email@address.com", "strong!password");

            //select item
            _driver.FindElement(By.LinkText("Shop")).Click();

            //add to cart
            shopPage.AddPoloToCart();

            //checkout
            _driver.FindElement(By.CssSelector("a[href*='cart']")).Click();

            cartPage.ProceedToCheckout();

            //complete billing details (valid postcode used here)
            checkoutPage.CompleteBillingDetails("Mister", "Grinch", "Mount Crumpit", "Whoville", "SW1A 1AA", "+44 (0) 330 606 0547"); //enter billing information

            //select 'Check payments' method
            checkoutPage.SelectPaymentMethod("Check payments");

            //place order
            string orderNumber = checkoutPage.PlaceOrder(); //capture order number

            //log order number to the console
            Console.WriteLine($"Order Number: {orderNumber}");

            //navigate to my account ---> orders to verify the order
            accountPage.NavigateToOrders();

            //check if the order number is present in this page/section
            bool isOrderPresent = accountPage.IsOrderPresent(orderNumber);
            Assert.That(isOrderPresent, Is.True, $"Order {orderNumber} not found in 'My Orders' section."); //verify order is listed

            //log success message if both assertions pass
            Console.WriteLine($"Test passed: Order {orderNumber} found in 'My Orders' section.");

            //logout from the account to complete test case
            accountPage.Logout();
        }

        //clean up after each test
        [TearDown]
        public void TearDown()
        {
            _driver.Quit(); //close browser after each test
            //could add waiting method before shutdown, but not required
        }

        //coding page object models for each page throughout the test journey

        public class LoginPage
        {
            private readonly IWebDriver _driver; //private variable to hold the WebDriver instance
            public LoginPage(IWebDriver driver) => _driver = driver; //constructor to assign driver

            //locate all elements needed to perform login
            private IWebElement EmailField => _driver.FindElement(By.Id("username")); //email input field
            private IWebElement PasswordField => _driver.FindElement(By.Id("password")); //pwsd field
            private IWebElement LoginButton => _driver.FindElement(By.CssSelector("button[name='login']")); //login button

            //method to perform login action
            public void Login(string username, string password)
            {
                //enter text, click login
                EmailField.SendKeys(username);
                PasswordField.SendKeys(password);
                LoginButton.Click();
            }
        }

        public class ShopPage
        {
            private readonly IWebDriver _driver;
            public ShopPage(IWebDriver driver) => _driver = driver; 

            //locate elements to select and add polo to the cart
            private IWebElement PoloItem => _driver.FindElement(By.PartialLinkText("Polo"));
            private IWebElement AddToCartButton => _driver.FindElement(By.CssSelector("button[name='add-to-cart']"));

            //method to add polo to the cart
            public void AddPoloToCart()
            {
                PoloItem.Click();
                AddToCartButton.Click();
            }
        }

        public class CartPage
        {
            private readonly IWebDriver _driver;
            private readonly WebDriverWait _wait; //wait instance for explicit waits

            public CartPage(IWebDriver driver)
            {
                _driver = driver;
                _wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(10)); //set explicit wait for elements on cart page
            }

            //locate elements on cart page
            private IWebElement CouponField => _driver.FindElement(By.Id("coupon_code"));
            private IWebElement ApplyCouponButton => _driver.FindElement(By.CssSelector("button[name='apply_coupon']"));
            private IWebElement CouponDiscount => _wait.Until(drv => drv.FindElement(By.CssSelector(".cart-discount.coupon-edgewords td span"))); //discount amount after coupon
            private IWebElement Subtotal => _wait.Until(drv => drv.FindElement(By.CssSelector(".cart-collaterals .cart-subtotal td span"))); 
            private IWebElement Shipping => _wait.Until(drv => drv.FindElement(By.CssSelector("#shipping_method span bdi"))); 
            private IWebElement Total => _wait.Until(drv => drv.FindElement(By.CssSelector(".order-total td strong span bdi"))); 

            //method to apply coupon code (enter text and click)
            public void ApplyCoupon(string couponCode)
            {
                CouponField.SendKeys(couponCode);
                ApplyCouponButton.Click();
            }

            //method to retrieve and parse cart totals for validation
            public (decimal subtotal, decimal discount, decimal shipping, decimal total) GetCartTotals()
            {
                //parse values - remove currency symbol and need to strip out any whitespace
                //parse string into a decimal and ensure consistent handling regardless of the user culture settings (using InvariantCulture)
                decimal subtotal = decimal.Parse(Subtotal.Text.Replace("£", "").Trim(), CultureInfo.InvariantCulture); 
                decimal discount = decimal.Parse(CouponDiscount.Text.Replace("£", "").Trim(), CultureInfo.InvariantCulture);
                decimal shipping = decimal.Parse(Shipping.Text.Replace("£", "").Trim(), CultureInfo.InvariantCulture); 
                decimal total = decimal.Parse(Total.Text.Replace("£", "").Trim(), CultureInfo.InvariantCulture);
                return (subtotal, discount, shipping, total); //return parsed values
            }

            //method to proceed to checkout after adding items to the cart
            public void ProceedToCheckout()
            {
                //locate checkout button and click ---> proceed to checkout page
                _driver.FindElement(By.CssSelector("body > p > a")).Click(); //obstruction in front of checkout button, caused an error - so removed by clicking 'dismiss'
                _driver.FindElement(By.PartialLinkText("checkout")).Click(); //click checkout
            }
        }

        public class CheckoutPage
        {
            private readonly IWebDriver _driver; 
            private readonly WebDriverWait _wait;

            public CheckoutPage(IWebDriver driver)
            {
                _driver = driver; 
                _wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(10));
            }

            //method to complete billing details during checkout
            public void CompleteBillingDetails(string firstName, string lastName, string address, string city, string postcode, string phoneNumber)
            {
                //input fields for billing details
                var NameField = _driver.FindElement(By.CssSelector("#billing_first_name")); 
                var SurnameField = _driver.FindElement(By.CssSelector("#billing_last_name")); 
                var AddressField = _driver.FindElement(By.CssSelector("#billing_address_1")); 
                var CityField = _driver.FindElement(By.CssSelector("#billing_city")); 
                var PostcodeField = _driver.FindElement(By.CssSelector("#billing_postcode"));
                var PhoneField = _driver.FindElement(By.CssSelector("#billing_phone")); 


                //clear any existing values in the fields
                NameField.Clear(); 
                SurnameField.Clear(); 
                AddressField.Clear();
                CityField.Clear(); 
                PostcodeField.Clear();
                PhoneField.Clear(); 

                //enter billing information
                NameField.SendKeys(firstName); 
                SurnameField.SendKeys(lastName);
                AddressField.SendKeys(address); 
                CityField.SendKeys(city); 
                PostcodeField.SendKeys(postcode); 
                PhoneField.SendKeys(phoneNumber);
            }

            //method to select payment method
            public void SelectPaymentMethod(string paymentMethod)
            {
                //locate payment method option and select it (tried xpath instead of css here)
                var paymentOption = _driver.FindElement(By.XPath($"//label[contains(text(), '{paymentMethod}')]"));
                paymentOption.Click();
            }

            //method to place the order and return the order number
            public string PlaceOrder()
            {
                var PlaceOrderButton = _driver.FindElement(By.Id("place_order")); 
                PlaceOrderButton.Click(); 

                //wait for order confirmation message and extract order number
                var orderNumberElement = _wait.Until(drv => drv.FindElement(By.CssSelector("#post-6 .woocommerce-order-overview__order > strong"))); //waiting for order number element to appear
                return orderNumberElement.Text; //return order number text
            }
        }

        public class AccountPage
        {
            private readonly IWebDriver _driver;
            private readonly WebDriverWait _wait;
            public AccountPage(IWebDriver driver) => _driver = driver;

            //locate account and logout elements
            private IWebElement AccountLink => _driver.FindElement(By.PartialLinkText("account"));
            private IWebElement LogoutLink => _driver.FindElement(By.LinkText("Log out"));
            private IWebElement OrdersLink => _driver.FindElement(By.LinkText("Orders"));

            //method to perform logout action
            public void Logout()
            {
                AccountLink.Click(); //navigate to account first
                LogoutLink.Click();
            }

            //method to navigate to the orders section in the account
            public void NavigateToOrders()
            {
                AccountLink.Click();
                OrdersLink.Click();
            }

            //method to check if a specific order number is present in orders section 
            public bool IsOrderPresent(string orderNumber)
            {
                try
                {
                    //locate the element containing the order number by checking the entire page
                    //I tried checking only the Orders table, but the locator was causing issues
                    var orderElement = _driver.FindElement(By.XPath($"//*[contains(text(), '{orderNumber}')]"));

                    //check if orderElement value was found and displayed
                    return orderElement.Displayed; //return true if it is
                }
                catch (NoSuchElementException) //catch exception if order is not found
                {
                    Console.WriteLine($"Order number {orderNumber} not found on the orders page.");
                    return false; //return false if not found
                }
                catch (Exception ex) //catch any other unexpected exceptions
                {
                    Console.WriteLine($"An unexpected error occurred: {ex.Message}");
                    return false; //return false for unexpected errors
                }
            }
        }

        
        
    }
}