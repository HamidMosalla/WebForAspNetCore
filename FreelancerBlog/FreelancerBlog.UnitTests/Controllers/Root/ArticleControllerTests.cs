using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using FakeItEasy;
using FluentAssertions;
using FreelancerBlog.Core.DomainModels;
using FreelancerBlog.Core.Queries.Data.Articles;
using FreelancerBlog.Web.Areas.Admin.ViewModels.Article;
using FreelancerBlog.Web.Controllers;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace FreelancerBlog.UnitTests.Controllers.Root
{
    public class ArticleControllerTests
    {
        private readonly IMediator _mediatorFake;
        private readonly IMapper _mapperFake;
        private readonly ArticleController _sut;

        public ArticleControllerTests()
        {
            _mediatorFake = A.Fake<IMediator>();
            _mapperFake = A.Fake<IMapper>();
            _sut = new ArticleController(_mapperFake, _mediatorFake);
        }

        [Fact]
        public async Task Index_Always_ReturnsTheCorrectView()
        {
            var result = (ViewResult)await _sut.Index();

            result.Should().NotBeNull();
            result.ViewName.Should().BeNull();
        }

        [Fact]
        public async Task Index_Always_PassesTheCorrectArticlesToMapMethod()
        {
            var articles = new[] { new Article { ArticleId = 1 } }.AsQueryable();

            A.CallTo(() => _mediatorFake.Send(A<GetArticlesQuery>.Ignored, A<CancellationToken>.Ignored)).Returns(articles);

            await _sut.Index();

            A.CallTo(() => _mapperFake.Map<IQueryable<Article>, List<ArticleViewModel>>(articles)).MustHaveHappened(1, Times.Exactly);
        }

        [Fact]
        public async Task Tag_IdIsEmpty_ReturnsBadRequest()
        {
            var result = (BadRequestResult)await _sut.Tag(0);

            result.Should().BeOfType<BadRequestResult>();
        }

        [Fact]
        public async Task Tag_IdNotEmpty_ReturnsTheCorrectView()
        {
            var result = (ViewResult)await _sut.Tag(1);

            result.ViewName.Should().BeNull();
        }

        [Fact]
        public async Task Tag_IdNotEmpty_ReturnsTheCorrectViewModel()
        {
            var viewModels = new[] { new ArticleViewModel { ArticleId = 1 } }.ToList();
            A.CallTo(() => _mapperFake.Map<IQueryable<Article>, List<ArticleViewModel>>(A<IQueryable<Article>>.Ignored)).Returns(viewModels);

            var result = (ViewResult)await _sut.Tag(1);

            result.ViewData.Model.Should().BeOfType<List<ArticleViewModel>>();
            result.ViewData.Model.Should().Be(viewModels);
        }

        [Fact]
        public async Task Tag_IdNotEmpty_PassesTheCorrectIdToArticlesByTagQuery()
        {
            var tagId = 1;
            var result = (ViewResult)await _sut.Tag(tagId);

            A.CallTo(() => _mediatorFake.Send(A<ArticlesByTagQuery>.That.Matches(a => a.TagId == tagId), A<CancellationToken>._)).MustHaveHappened();
        }
    }
}