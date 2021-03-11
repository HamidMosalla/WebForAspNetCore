using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using FreelancerBlog.Core.Commands.Data.ArticleComments;
using FreelancerBlog.Core.Commands.Data.Articles;
using FreelancerBlog.Core.Commands.Data.ArticleTags;
using FreelancerBlog.Core.DomainModels;
using FreelancerBlog.Core.Queries.Data.ArticleComments;
using FreelancerBlog.Core.Queries.Data.Articles;
using FreelancerBlog.Core.Queries.Data.ArticleTags;
using FreelancerBlog.Core.Services.Shared;
using FreelancerBlog.Web.Areas.Admin.ViewModels.Article;
using FreelancerBlog.Web.ViewModels.Article;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Mvc.JQuery.DataTables;

namespace FreelancerBlog.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "admin")]
    public class ArticleController : Controller
    {
        private readonly IMapper _mapper;
        private IMediator _mediator;
        private readonly IFileManager _fileManager;

        public ArticleController(IFileManager fileManager, IMapper mapper, IMediator mediator)
        {
            _fileManager = fileManager;
            _mapper = mapper;
            _mediator = mediator;
        }

        [HttpGet]
        public IActionResult ManageArticle() => View();

        [HttpGet]
        public async Task<DataTablesResult<ArticleViewModel>> GetArticleTableData(DataTablesParam dataTableParam)
        {
            var articles = await _mediator.Send(new GetArticlesQuery());

            var articleViewModels = articles.Select(a => new ArticleViewModel
            {
                ArticleId = a.ArticleId,
                ArticleDateModified = a.ArticleDateModified,
                ArticleDateCreated = a.ArticleDateCreated,
                ArticleStatus = a.ArticleStatus,
                ArticleTitle = a.ArticleTitle
            });

            return DataTablesResult.Create(articleViewModels, dataTableParam);
        }

        [HttpGet]
        public async Task<IActionResult> ManageArticleComment()
        {
            var comments = await _mediator.Send(new GetAllCommentsQuery { });

            var commentsViewModel = _mapper.Map<IQueryable<ArticleComment>, List<ArticleCommentViewModel>>(comments);

            return View(commentsViewModel);
        }

        [HttpGet]
        public async Task<IActionResult> ManageArticleTag()
        {
            var tags = await _mediator.Send(new GetAllArticleTagsQuery { });

            var tagsViewModel = _mapper.Map<IQueryable<ArticleTag>, List<ArticleTagViewModel>>(tags);

            return View(tagsViewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<JsonResult> DeleteArticleComment(int id)
        {
            if (id == default(int)) return Json(new { Status = "IdCannotBeNull" });

            var model = await _mediator.Send(new ArticleCommentByIdQuery { ArticleCommentId = id });

            if (model == null) return Json(new { Status = "ArticleCommentNotFound" });

            await _mediator.Send(new DeleteArticleCommentCommand { ArticleComment = model });

            return Json(new { Status = "Deleted" });
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<JsonResult> DeleteArticleTag(int id)
        {
            if (id == default(int)) return Json(new { Status = "IdCannotBeNull" });

            var model = await _mediator.Send(new FindArticleTagByIdQuery { ArticleTagId = id });

            if (model == null) return Json(new { Status = "ArticleCommentNotFound" });

            await _mediator.Send(new DeleteArticleTagCommand { ArticleTag = model });

            return Json(new { Status = "Deleted" });
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<JsonResult> ChangeArticleCommentApprovalStatus(int commentId)
        {
            if (commentId == default(int)) return Json(new { Status = "IdCannotBeNull" });

            var model = await _mediator.Send(new ArticleCommentByIdQuery { ArticleCommentId = commentId });

            if (model == null) return Json(new { Status = "ArticleCommentNotFound" });

            await _mediator.Send(new ToggleArticleCommentApprovalCommand { ArticleComment = model });

            return Json(new { Status = "Success" });
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ArticleViewModel viewModel)
        {
            if (!ModelState.IsValid) return View(viewModel);

            var model = _mapper.Map<ArticleViewModel, Article>(viewModel);

            await _mediator.Send(new CreateArticleCommand { Article = model });
            TempData["ViewMessage"] = "مقاله با موفقیت ثبت شد.";

            await _mediator.Send(new CreateArticleTagsCommand { Article = model, ArticleTags = viewModel.ArticleTags });
            TempData["ArticleTagCreateMessage"] = "تگ های جدید با موفقیت ثبت شدند.";

            return RedirectToAction("ManageArticle", "Article");
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            if (id == default(int)) return BadRequest();

            var article = await _mediator.Send(new ArticleByArticleIdQuery { ArticleId = id });

            if (article == null) return NotFound();

            var articleViewModel = _mapper.Map<Article, ArticleViewModel>(article);

            articleViewModel.ArticleTagsList = await _mediator.Send(new GetCurrentArticleTagsQuery { ArticleId = article.ArticleId });

            return View(articleViewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(ArticleViewModel viewModel)
        {
            if (!ModelState.IsValid) return View(viewModel);

            var article = _mapper.Map<ArticleViewModel, Article>(viewModel);

            await _mediator.Send(new UpdateArticleCommand {Article = article});
            TempData["ViewMessage"] = "مقاله با موفقیت ویرایش شد.";

             await _mediator.Send(new UpdateArticleTagsCommand { Article = article, ArticleTags = viewModel.ArticleTags });
            TempData["ArticleTagCreateMessage"] = "تگ های مقاله با موفقیت به روز رسانی شدند.";

            return RedirectToAction("ManageArticle", "Article");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<JsonResult> EditArticleComment(int commentId, string newCommentBody)
        {
            if (commentId == default(int)) return Json(new { Status = "IdCannotBeNull" });

            var model = await _mediator.Send(new ArticleCommentByIdQuery { ArticleCommentId = commentId });

            if (model == null) return Json(new { Status = "ArticleCommentNotFound" });

            await _mediator.Send(new EditArticleCommentCommand { ArticleComment = model, NewCommentBody = newCommentBody });

            return Json(new { Status = "Success" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<JsonResult> EditArticleTag(int tagId, string newTagName)
        {
            if (tagId == default(int)) return Json(new { Status = "IdCannotBeNull" });

            var model = await _mediator.Send(new FindArticleTagByIdQuery { ArticleTagId = tagId });

            if (model == null) return Json(new { Status = "ArticleTagNotFound" });

            await _mediator.Send(new EditArticleTagCommand { ArticleTag = model, NewTagName = newTagName });

            return Json(new { Status = "Success" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<JsonResult> Delete(int id)
        {
            if (id == default(int)) return Json(new { Status = "IdCannotBeNull" });

            var model = await _mediator.Send(new ArticleByArticleIdQuery { ArticleId = id });

            if (model == null) return Json(new { Status = "ArticleNotFound" });

            _fileManager.DeleteEditorImages(model.ArticleBody, new List<string> { "Files", "ArticleUploads" });

            await _mediator.Send(new DeleteArticleCommand { Article = model });

            return Json(new { Status = "Deleted" });
        }

        public async Task<IActionResult> TagLookup()
        {
            var model = await _mediator.Send(new GetAllTagNamesQuery());

            return Json(model);
        }

        [HttpPost]
        public async Task<IActionResult> CkEditorFileUploder(IFormFile file, string ckEditorFuncNum)
        {
            string htmlResult = await _fileManager.UploadFromCkEditorAsync(file, new List<string> { "images", "blog" }, ckEditorFuncNum);

            return Content(htmlResult, "text/html");
        }
    }
}
