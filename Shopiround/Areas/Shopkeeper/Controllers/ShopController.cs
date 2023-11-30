﻿using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Shopiround.Repository.IRepository;
using Shopiround.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Shopiround.Data;
using Shopiround.Repository;
using Shopiround.Models.Statistics;
using Microsoft.EntityFrameworkCore;

namespace Shopiround.Areas.Shopkeeper.Controllers
{
    [Area("Shopkeeper")]
    public class ShopController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;
        public readonly IWebHostEnvironment _webHostEnvironment;
        public ApplicationDbContext context;
        public ShopController(IUnitOfWork unitOfWork, IWebHostEnvironment webHostEnvironment, ApplicationDbContext context)
        {
            _unitOfWork = unitOfWork;
            _webHostEnvironment = webHostEnvironment;
            this.context = context;
        }
        public IActionResult Index()
        {
            var topProductCounts = context.ProductCounts
            .OrderByDescending(pc => pc.Count)
            .Take(5).Include("Product")
            .ToList();

            var MostSearchedKeyword = context.KeywordsCounts
            .OrderByDescending(pc => pc.Count)
            .Take(5)
            .ToList();

            ViewData["ProductCount"] = topProductCounts;
            ViewData["KeywordCount"] = MostSearchedKeyword;

            return View();
        }

        public IActionResult ShowAllPopularProduct()
        {
            var topProductCounts = context.ProductCounts
            .OrderByDescending(pc => pc.Count)
            .Include("Product").Include("Shop")
            .ToList();

            ViewData["ProductCount"] = topProductCounts;

            return View();
        }

        public IActionResult CustomerPopularProduct()
        {
            var topProductCounts = context.ProductCounts
            .OrderByDescending(pc => pc.Count)
            .Include("Product")
            .ToList();

            ViewData["ProductCount"] = topProductCounts;

            return View();
        }
        public IActionResult ShowAllKeywords()
        {
            var MostSearchedKeyword = context.KeywordsCounts
            .OrderByDescending(pc => pc.Count)
            .ToList();

            ViewData["KeywordCount"] = MostSearchedKeyword;

            return View();
        }
        [Authorize]
        public IActionResult Create()
        {
            ApplicationUser applicationUser = _unitOfWork.ApplicationUserRepository.Get(u => u.UserName == User.Identity.Name);
            if (applicationUser == null)
            {
                return new RedirectToPageResult("/Identity/Account/Login");
            }
            var viewModel = new Shop
            {
                OwnerName = applicationUser.Name
            };
            return View(viewModel);
        }
        [HttpPost]
        public IActionResult Create(Shop shop, IFormFile? file)
        {
            if (file != null)
            {
                string wwwRootPath = _webHostEnvironment.WebRootPath;
                string fileName = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName);
                string shopPath = Path.Combine(wwwRootPath, @"images\shop");
                using (var fileStream = new FileStream(Path.Combine(shopPath, fileName), FileMode.Create))
                {
                    file.CopyTo(fileStream);
                };
                shop.ImageURL = @"\images\shop\" + fileName;
            }
            else
            {
                shop.ImageURL = @"\images\shop\default_shop.png";
            }
            ApplicationUser applicationUser = _unitOfWork.ApplicationUserRepository.Get(u => u.UserName == User.Identity.Name);
            _unitOfWork.ShopRepository.Add(shop);
            applicationUser.Shop = shop;
            _unitOfWork.ApplicationUserRepository.Update(applicationUser);
            _unitOfWork.Save();
            return RedirectToAction("Index");
        }

        public IActionResult Profile()
        {
            ApplicationUser applicationUser = _unitOfWork.ApplicationUserRepository.Get(u => u.UserName == User.Identity.Name, includeProperties: "Shop");
            Shop shop = applicationUser.Shop;
            List<Product> products = context.Products.Where(p => p.ShopId == shop.ShopId).ToList();

            ShopProfile shopProfile = new ShopProfile
            { 
                shop = shop,
                products = products
            };

            return View(shopProfile);
        }
        public IActionResult ShowUserInformation()
        {
            return View();
        }

        public IActionResult OnlineOrdersHome()
        {
            ApplicationUser applicationUser = _unitOfWork.ApplicationUserRepository.Get(u => u.UserName == User.Identity.Name, includeProperties: "Shop");
            List<Product> products = context.Products.Where(p => p.ShopId == applicationUser.Shop.ShopId).ToList();
            List<CartItem> cartItems = context.CartItems.Include(c => c.Product).Include(c => c.User).Where(c => products.Contains(c.Product) && c.Online).ToList();
            List<OnlineOrderVM> onlineOrders = (from cart in cartItems
                    group cart by cart.UserId into g
                    select new OnlineOrderVM
                    {
                        User = context.ApplicationUsers.Where(u => u.Id == g.Key).First(),
                        TotalOrders = g.Count(),
                        TotalPrice = (int)g.Sum(c => c.Product.Price)
                    }).ToList();
            onlineOrders.Reverse();
            return View(onlineOrders);
        }
        [Authorize]
        public IActionResult DoneOnlineShopping(string userId)
        {
            ApplicationUser user = _unitOfWork.ApplicationUserRepository.Get(u => u.UserName == userId, includeProperties: "Shop,CartItems");
            List<CartItem> cartItems = context.CartItems.Include(c => c.Product).ThenInclude(s => s.Shop).Where(c => c.UserId == userId && c.Online).ToList();
            foreach (CartItem cartItem in cartItems)
            {
                PurchaseItem purchaseItem = new PurchaseItem
                {
                    UserId = cartItem.UserId,
                    ProductId = cartItem.ProductId,
                    Quantity = cartItem.Quantity,
                    Online = cartItem.Online,
                    Reserve = cartItem.Reserve,
                    PurchaseDate = DateTime.Now
                };
                context.PurchaseItems.Add(purchaseItem);
            }
            context.CartItems.RemoveRange(cartItems);
            context.SaveChanges();
            return RedirectToAction("Index");
        }

        public IActionResult OnlineOrder(string userId)
        {
            List<CartItem> cartItems = context.CartItems.Where(c => c.Online).Include(c => c.Product).ThenInclude(s => s.Shop).Where(c => c.UserId == userId).ToList();
            ViewBag.userId = userId;
            return View(cartItems);
        }

    }
}
