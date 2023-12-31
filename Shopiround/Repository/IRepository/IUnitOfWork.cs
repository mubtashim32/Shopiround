﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shopiround.Repository.IRepository
{
    public interface IUnitOfWork
    {
        IApplicationUserRepository ApplicationUserRepository { get; }
        IShopRepository ShopRepository { get; }
        IProductRepository ProductRepository { get; }
        IReviewRepository ReviewRepository { get; }
        IQuestionRepository QuestionRepository { get; }
        IAnswerRepository AnswerRepository { get; }
        ICartItemRepository CartItemRepository { get; }
        void Save();
    }
}
