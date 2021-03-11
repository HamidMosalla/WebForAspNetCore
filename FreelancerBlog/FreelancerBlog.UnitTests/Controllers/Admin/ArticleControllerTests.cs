using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using FakeItEasy;
using FluentAssertions;
using FreelancerBlog.Core.DomainModels;
using FreelancerBlog.Core.Queries.Data.Articles;
using FreelancerBlog.Core.Services.Shared;
using FreelancerBlog.Web.Areas.Admin.Controllers;
using FreelancerBlog.Web.Areas.Admin.ViewModels.Article;
using FreelancerBlog.Web.ViewModels.Article;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Mvc.JQuery.DataTables;
using Xunit;

namespace FreelancerBlog.UnitTests.Controllers.Admin
{
    public class ArticleControllerTests
    {
        private readonly IFileManager _fileManager;
        private readonly IMapper _mapper;
        private IMediator _mediator;
        private ArticleController _sut;

        public ArticleControllerTests()
        {
            _mapper = A.Fake<IMapper>();
            _mediator = A.Fake<IMediator>();
            _fileManager = A.Fake<IFileManager>();
            _sut = new ArticleController(_fileManager, _mapper, _mediator);
        }

        [Fact]
        public async Task GetArticleTableData_Should_ReturnTheCorrectType()
        {
            var articles = new List<Article> { new Article(), new Article(), new Article() }.AsQueryable();

            A.CallTo(() => _mediator.Send(A<GetArticlesQuery>._, A<CancellationToken>._)).Returns(articles);

            var result = await _sut.GetArticleTableData(new DataTablesParam());

            result.Should().BeOfType<DataTablesResult<ArticleViewModel>>();
        }

        [Fact]
        public async Task ManageArticleComment_ShouldReturn_TheCorrectType()
        {
            var result = await _sut.ManageArticleComment() as ViewResult;

            A.CallTo(() => _mapper.Map<IQueryable<ArticleComment>, List<ArticleCommentViewModel>>(new List<ArticleComment>{ new ArticleComment()}.AsQueryable()))
                .Returns(new List<ArticleCommentViewModel>());

            result.Should().NotBeNull();
            var resultModel = result.Model as List<ArticleCommentViewModel>;
            resultModel.Should().NotBeNull();
        }
    }
}
