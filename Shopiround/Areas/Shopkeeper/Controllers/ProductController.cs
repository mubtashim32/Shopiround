﻿using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Shopiround.Repository;
using Shopiround.Repository.IRepository;
using Shopiround.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Shopiround.Data;
using Shopiround.Models.Statistics;
using System.Threading;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics.Contracts;

namespace Shopiround.Areas.Shopkeeper.Controllers
{
    [Area("Shopkeeper")]
    public class ProductController : Controller
    {

        private readonly IUnitOfWork unitOfWork;
        private readonly IWebHostEnvironment webHostEnvironment;

        private readonly ApplicationDbContext applicationDbContext;

        public ProductController(IUnitOfWork unitOfWork, IWebHostEnvironment webHostEnvironment, ApplicationDbContext applicationDbContext)
        {
            this.unitOfWork = unitOfWork;
            this.webHostEnvironment = webHostEnvironment;
            this.applicationDbContext = applicationDbContext;
        }

        public IActionResult Index()
        {
            
            // User and Shop information
            ApplicationUser? user = applicationDbContext.ApplicationUsers.Where(x => x.UserName == User.Identity.Name).FirstOrDefault();
            Shop? shop = null;
            if (user != null)
            {
                shop = applicationDbContext.Shops.Where(x => x.ApplicationUserId == user.Id).FirstOrDefault();
            }
            List<Product> products = applicationDbContext.Products.Include("Shop").Where(p => p.Shop == shop).ToList();
            ViewBag.user = user;
            ViewBag.shop = shop;
            return View(products);
        }

        public IActionResult Create()
        {
            // User and Shop information
            ApplicationUser? user = applicationDbContext.ApplicationUsers.Where(x => x.UserName == User.Identity.Name).FirstOrDefault();
            Shop? shop = null;
            if (user != null)
            {
                shop = applicationDbContext.Shops.Where(x => x.ApplicationUserId == user.Id).FirstOrDefault();
            }
            ViewBag.user = user;
            ViewBag.shop = shop;
            return View();
        }
        [HttpGet]
        public IActionResult AddDiscount()
        {
            ApplicationUser user = unitOfWork.ApplicationUserRepository.Get(u => u.UserName == User.Identity.Name, includeProperties: "CartItems,Shop");
            Shop shop = user.Shop;
            List<Product> products = unitOfWork.ProductRepository.GetAllCondition(p => p.ShopId == shop.ShopId).ToList();
            List<DiscountVM> discountVMs = new List<DiscountVM>();

            foreach (Product product in products)
            {
                discountVMs.Add(new DiscountVM
                {
                    ID = product.Id,
                    Name = product.Name,
                    ImageURL = product.ImageURL,
                    Price = product.Price,
                    DiscountAmount = product.DiscountAmount,
                    DiscountParcentage = product.DiscountPercentage
                });
            }
            // User and Shop information
            ViewBag.user = user;
            ViewBag.shop = shop;
            return View(discountVMs);
        }

        [HttpPost]
        public IActionResult AddDiscount(DiscountVM discountVM)
        {
            DateTime today = DateTime.Now;
            DateTime futureDate = today.AddDays(discountVM.TotalDays);

            Product productx = applicationDbContext.Products.Where(p => p.Id == discountVM.ID).FirstOrDefault();
            productx.DiscountAmount = discountVM.DiscountAmount;
            productx.DiscountPercentage = discountVM.DiscountParcentage;

            applicationDbContext.Products.Update(productx);

            DiscountDate discountDate = new DiscountDate()
            {
                productId = discountVM.ID,
                discountEndDate = futureDate,
                TodayDiscount = discountVM.TodayDiscount

            };
            applicationDbContext.DiscountDates.Add(discountDate);
            applicationDbContext.SaveChanges();

            ApplicationUser user = unitOfWork.ApplicationUserRepository.Get(u => u.UserName == User.Identity.Name, includeProperties: "CartItems,Shop");
            Shop shop = user.Shop;
            List<Product> products = unitOfWork.ProductRepository.GetAllCondition(p => p.ShopId == shop.ShopId).ToList();
            List<DiscountVM> discountVMs = new List<DiscountVM>();

            foreach (Product product in products)
            {
                discountVMs.Add(new DiscountVM
                {
                    ID = product.Id,
                    Name = product.Name,
                    ImageURL = product.ImageURL,
                    Price = product.Price,
                    DiscountAmount = product.DiscountAmount,
                    DiscountParcentage = product.DiscountPercentage
                });
            }
            // User and Shop information
            ViewBag.user = user;
            ViewBag.shop = shop;
            return View(discountVMs);
        }

        [HttpPost]
        public IActionResult Create(Product product, IFormFile? file)
        {
            if (product.DiscountAmount == null)
            {
                product.DiscountAmount = 0;
            }
            if (product.DiscountPercentage == null)
            {
                product.DiscountAmount = 0;
            }
            if (file != null)
            {
                string wwwRootPath = webHostEnvironment.WebRootPath;
                string fileName = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName);
                string productPath = Path.Combine(wwwRootPath, @"images\product");
                using (var fileStream = new FileStream(Path.Combine(productPath, fileName), FileMode.Create))
                {
                    file.CopyTo(fileStream);
                };
                product.ImageURL = @"\images\product\" + fileName;
            }
            else
            {
                product.ImageURL = @"\images\shop\default_shop.png";
            }
            ApplicationUser user = unitOfWork.ApplicationUserRepository.Get(u => u.UserName == User.Identity.Name);
            product.ShopId = unitOfWork.ShopRepository.Get(s => s.applicationUser == user).ShopId;
            unitOfWork.ProductRepository.Add(product);
            unitOfWork.Save();

            // User and Shop information
            Shop? shop = null;
            if (user != null)
            {
                shop = applicationDbContext.Shops.Where(x => x.ApplicationUserId == user.Id).FirstOrDefault();
            }
            ViewBag.user = user;
            ViewBag.shop = shop;

            return RedirectToAction("Index");
        }

        public IActionResult Show(int? Id)
        {
            if (Id != null)
            {
                Product product = unitOfWork.ProductRepository.Get(p => p.Id == Id, includeProperties: "Shop,Reviews");

                DiscountDate discountDate = applicationDbContext.DiscountDates.Where(d => d.productId == product.Id).FirstOrDefault();


                ApplicationUser user = unitOfWork.ApplicationUserRepository.Get(u => u.UserName == User.Identity.Name);
                Boolean AlreadyInCart = false;
                int cartCount = 0;
                CartItem cart = null;
                if (user != null)
                {
                    cart = applicationDbContext.CartItems.Where(a => a.UserId == user.Id && a.ProductId == product.Id).FirstOrDefault();
                }
                
                if (cart != null)
                {
                    AlreadyInCart = true;
                    cartCount = cart.Quantity;
                }
                else
                {
                    AlreadyInCart = false;
                    cartCount = 1;
                }

                ViewBag.CartCount = cartCount;
                ViewBag.AlreadyInCart = AlreadyInCart;


                if (product != null)
                {
                    ProductCount productCount = applicationDbContext.ProductCounts.FirstOrDefault(a => a.ProductId == Id);
                    if (productCount != null)
                    {
                        productCount.Count++;
                        applicationDbContext.ProductCounts.Update(productCount);
                    }
                    else
                    {
                        ProductCount productCount1 = new ProductCount()
                        {
                            ProductId = (int)Id,
                            Count = 1
                        };
                        applicationDbContext.ProductCounts.Add(productCount1);
                    }
                    applicationDbContext.SaveChanges();
                    // User and Shop information
                    Shop? shop = null;
                    if (user != null)
                    {
                        shop = applicationDbContext.Shops.Where(x => x.ApplicationUserId == user.Id).FirstOrDefault();
                    }
                    ViewBag.user = user;
                    ViewBag.shop = shop;
                    return View(product);
                }
                else
                {
                    return NotFound();
                }
            }
            else
            {
                return NotFound();
            }
        }

        public IActionResult ShowAllProducts()
        {
            ApplicationUser user = unitOfWork.ApplicationUserRepository.Get(u => u.UserName == User.Identity.Name, includeProperties: "CartItems,Shop");
            Shop shop = user.Shop;
            List<Product> products = unitOfWork.ProductRepository.GetAllCondition(p => p.ShopId == shop.ShopId).ToList();
            // User and Shop information
            ViewBag.user = user;
            ViewBag.shop = shop;
            return View(products);
        }

        [HttpGet]
        public IActionResult Edit(int id)
        {
            Product product = applicationDbContext.Products.Where(a=> a.Id == id).FirstOrDefault();
            return View(product);
        }

        [HttpPost]
        public IActionResult Edit(Product product, IFormFile? file)
        {
            Product _product = applicationDbContext.Products.Where(a=> a.Id == product.Id).FirstOrDefault();

            if (file != null && product != null)
            {
                string wwwRootPath = webHostEnvironment.WebRootPath;
                string fileName = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName);
                string productPath = Path.Combine(wwwRootPath, @"images\product");
                using (var fileStream = new FileStream(Path.Combine(productPath, fileName), FileMode.Create))
                {
                    file.CopyTo(fileStream);
                };
                _product.ImageURL = @"\images\product\" + fileName;

                _product.Name = product.Name;
                _product.Description = product.Description;
                _product.Price = product.Price;
                _product.DiscountPercentage = product.DiscountPercentage;
                _product.DiscountAmount = product.DiscountAmount;
                _product.Category = product.Category;


                applicationDbContext.Products.Update(_product);
                applicationDbContext.SaveChanges();
            }


            return RedirectToAction("Index");
        }


        public IActionResult SearchResult(string name)
        {
            if (name != null)
            {
                string searchTerm = name;
                var searchWords = searchTerm.ToLower().Split(' ');

                var matchingProducts = unitOfWork.ProductRepository
                    .GetAll()
                    .Where(product => searchWords.Any(word => product.Name.ToLower().Contains(word)))
                    .ToList();
                // List<Product> searchedProducts = unitOfWork.ProductRepository.GetAll().Where(j => j.Name.ToLower().Contains(name.ToLower())).ToList();
                // User and Shop information
                ApplicationUser? user = applicationDbContext.ApplicationUsers.Where(x => x.UserName == User.Identity.Name).FirstOrDefault();
                Shop? shop = null;
                if (user != null)
                {
                    shop = applicationDbContext.Shops.Where(x => x.ApplicationUserId == user.Id).FirstOrDefault();
                }
                ViewBag.user = user;
                ViewBag.shop = shop;
                return View(matchingProducts);
            }
            else
            {
                return NotFound();
            }
        }

        [HttpPost]
        public ActionResult CreateReview(int ProductId, string ReviewText, int Rating)
        {
            ApplicationUser? user = applicationDbContext.ApplicationUsers.Where(x => x.UserName == User.Identity.Name).FirstOrDefault();
            Product product = unitOfWork.ProductRepository.Get(p => p.Id == ProductId, includeProperties: "Reviews,Shop");
            Review review = new Review();
            review.ProductId = ProductId;
            review.Text = ReviewText;
            review.Reviewer = user.Name;
            review.SerialNo = 1;
            review.Rating = Rating;

            unitOfWork.ReviewRepository.Add(review);
            unitOfWork.Save();
            // User and Shop information
            
            Shop? shop = null;
            if (user != null)
            {
                shop = applicationDbContext.Shops.Where(x => x.ApplicationUserId == user.Id).FirstOrDefault();
            }
            ViewBag.user = user;
            ViewBag.shop = shop;
            return Json(new { ReviewText });
        }

        [HttpPost]
        public ActionResult CreateCartItem(int ProductId, int Quantity)
        {
            Product product = unitOfWork.ProductRepository.Get(p => p.Id == ProductId);
            ApplicationUser user = unitOfWork.ApplicationUserRepository.Get(u => u.UserName == User.Identity.Name);

            var cart = applicationDbContext.CartItems.Where(a => a.UserId == user.Id && a.ProductId == product.Id).FirstOrDefault();
            if (cart != null) unitOfWork.CartItemRepository.Remove(cart);

            CartItem cartItem = new CartItem
            {
                ProductId = ProductId,
                Product = product,
                UserId = user.Id,
                User = user,
                Quantity = Quantity,
                Online = false
            };
            unitOfWork.CartItemRepository.Add(cartItem);
            unitOfWork.Save();
            // User and Shop information
            Shop? shop = null;
            if (user != null)
            {
                shop = applicationDbContext.Shops.Where(x => x.ApplicationUserId == user.Id).FirstOrDefault();
            }
            ViewBag.user = user;
            ViewBag.shop = shop;
            return Json(new { added = true });
        }




        [HttpPost]
        public ActionResult OnlineCartItem(int ProductId)
        {
            Product product = unitOfWork.ProductRepository.Get(p => p.Id == ProductId, includeProperties: "Reviews,Shop");
            ApplicationUser user = unitOfWork.ApplicationUserRepository.Get(u => u.UserName == User.Identity.Name, includeProperties: "CartItems,Shop");
            CartItem cartItem = new CartItem
            {
                ProductId = ProductId,
                Product = product,
                UserId = user.Id,
                User = user,
                Quantity = 1,
                Online = true
            };
            unitOfWork.CartItemRepository.Add(cartItem);
            unitOfWork.Save();
            // User and Shop information
            Shop? shop = null;
            if (user != null)
            {
                shop = applicationDbContext.Shops.Where(x => x.ApplicationUserId == user.Id).FirstOrDefault();
            }
            ViewBag.user = user;
            ViewBag.shop = shop;
            return Json(new { added = true });
        }

        //Discount only for today.
        public ActionResult FlashSales()
        {

            var discountedProducts = applicationDbContext.Products
            .Where(p => applicationDbContext.DiscountDates.Any(d => d.productId == p.Id && d.discountEndDate > DateTime.Now))
            .ToList();
            // User and Shop information
            ApplicationUser? user = applicationDbContext.ApplicationUsers.Where(x => x.UserName == User.Identity.Name).FirstOrDefault();
            Shop? shop = null;
            if (user != null)
            {
                shop = applicationDbContext.Shops.Where(x => x.ApplicationUserId == user.Id).FirstOrDefault();
            }
            ViewBag.user = user;
            ViewBag.shop = shop;
            return View(discountedProducts);
        }

        public ActionResult DailyDeals()
        {
            var discountedProducts = applicationDbContext.Products
           .Where(p => applicationDbContext.DiscountDates.Any(d => d.productId == p.Id && d.TodayDiscount)).Include(p => p.Shop)
           .ToList();
            // User and Shop information
            ApplicationUser? user = applicationDbContext.ApplicationUsers.Where(x => x.UserName == User.Identity.Name).FirstOrDefault();
            Shop? shop = null;
            if (user != null)
            {
                shop = applicationDbContext.Shops.Where(x => x.ApplicationUserId == user.Id).FirstOrDefault();
            }
            ViewBag.user = user;
            ViewBag.shop = shop;
            return View(discountedProducts);
        }


    }
}
