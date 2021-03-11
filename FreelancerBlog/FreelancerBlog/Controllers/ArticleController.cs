using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using FreelancerBlog.Core.Commands.Data.ArticleComments;
using FreelancerBlog.Core.Commands.Data.Articles;
using FreelancerBlog.Core.DomainModels;
using FreelancerBlog.Core.Queries.Data.Articles;
using FreelancerBlog.Core.Queries.Services.Shared;
using FreelancerBlog.Core.Types;
using FreelancerBlog.Web.Areas.Admin.ViewModels.Article;
using FreelancerBlog.Web.Features.Queries;
using FreelancerBlog.Web.ViewModels.Article;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace FreelancerBlog.Web.Controllers
{
    public class ArticleController : Controller
    {
        private readonly IMapper _mapper;
        private readonly IMediator _mediator;

        public ArticleController(IMapper mapper, IMediator mediator)
        {
            _mapper = mapper;
            _mediator = mediator;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var articles = await _mediator.Send(new GetArticlesQuery());

            var articlesViewModel = _mapper.Map<IQueryable<Article>, List<ArticleViewModel>>(articles);

            return View(articlesViewModel);
        }

        [HttpGet]
        public async Task<IActionResult> Tag(int id)
        {
            if (id == default(int)) return BadRequest();

            var articles = await _mediator.Send(new ArticlesByTagQuery { TagId = id });

            var articlesViewModel = _mapper.Map<IQueryable<Article>, List<ArticleViewModel>>(articles);

            return View(articlesViewModel);
        }

        [HttpGet]
        public async Task<IActionResult> Detail(int id)
        {
            if (id == default(int)) return BadRequest();

            var article = await _mediator.Send(new ArticleByArticleIdQuery { ArticleId = 4 });

            if (article == null) return NotFound();

            await _mediator.Send(new IncreaseArticleViewCountCommand { ArticleId = id });

            var articleViewModel = await _mediator.Send(new ArticleViewModelQuery { Article = article, User = User });

            return View(articleViewModel);
        }

        public async Task<JsonResult> RateArticle(int id, double rating)
        {
            if (!User.Identity.IsAuthenticated) return Json(new { Status = "YouMustLogin" });

            var rateBefore = await _mediator.Send(new ArticleRatedBeforeQuery { ArticleId = id, User = User });

            if (rateBefore)
            {
                await _mediator.Send(new UpdateArticleRatingCommand { ArticleId = id, ArticleRating = rating, User = User });

                return Json(new { Status = "UpdatedSuccessfully" });
            }

            await _mediator.Send(new AddRatingToArticleCommand { ArticleId = id, ArticleRating = rating, User = User });

            return Json(new { Status = "Success" });
        }

        [HttpPost]
        public async Task<JsonResult> SubmitComment(ArticleCommentViewModel viewModel)
        {
            CaptchaResponse captchaResult = await _mediator.Send(new ValidateCaptchaQuery());

            if (captchaResult.Success != "true") return Json(new { status = "FailedTheCaptchaValidation" });

            if (!ModelState.IsValid) return Json(new { Status = "CannotHaveEmptyArgument" });

            var articleComment = _mapper.Map<ArticleCommentViewModel, ArticleComment>(viewModel);

            await _mediator.Send(new AddCommentToArticleCommand { ArticleComment = articleComment });

            return Json(new { Status = "Success" });
        }
    }
}