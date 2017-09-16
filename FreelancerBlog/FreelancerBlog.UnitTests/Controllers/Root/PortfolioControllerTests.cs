﻿using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using FakeItEasy;
using FluentAssertions;
using FreelancerBlog.Areas.Admin.ViewModels.Portfolio;
using FreelancerBlog.Controllers;
using FreelancerBlog.Core.Domain;
using FreelancerBlog.Core.Queries.Data.Portfolios;
using FreelancerBlog.Features.Commands;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace FreelancerBlog.UnitTests.Controllers.Root
{
    public class PortfolioControllerTests
    {
        private readonly IMediator _mediatorFake;
        private readonly IMapper _mapperFake;

        public PortfolioControllerTests()
        {
            _mediatorFake = A.Fake<IMediator>();
            _mapperFake = A.Fake<IMapper>();
        }

        [Fact]
        public async Task Detail_IdIsNotSupplied_ReturnsBadRequest()
        {
            //Arrange
            var sut = new PortfolioController(_mapperFake, _mediatorFake);

            //Act
            var result = (BadRequestResult)await sut.Detail(default(int));

            //Assert
            result.Should().NotBeNull();
            result.Should().BeOfType<BadRequestResult>();
            result.StatusCode.Should().Be(400);
        }

        [Fact]
        public async Task Detail_IdSupplied_PassesTheCorrectIdIntoPortfolioByIdQuery()
        {
            //Arrange
            var sut = new PortfolioController(_mapperFake, _mediatorFake);

            //Act
            var portfolioId = 2;
            await sut.Detail(portfolioId);

            //Assert
            A.CallTo(() => _mediatorFake.Send(A<PortfolioByIdQuery>.That.Matches(p => p.PortfolioId == portfolioId), A<CancellationToken>._)).MustHaveHappened();
        }

        [Fact]
        public async Task Detail_PorfolioDetailNotFound_ReturnNotFound()
        {
            //Arrange
            var sut = new PortfolioController(_mapperFake, _mediatorFake);

            A.CallTo(() => _mediatorFake.Send(A<PortfolioByIdQuery>._, A<CancellationToken>._)).Returns((Portfolio)null);

            //Act
            var result = (NotFoundResult)await sut.Detail(1);

            //Assert
            result.Should().NotBeNull();
            result.Should().BeOfType<NotFoundResult>();
            result.StatusCode.Should().Be(404);
        }

        [Fact]
        public async Task Detail_ModelNotNull_PassesTheCorrectPortfolioToMap()
        {
            //Arrange
            var sut = new PortfolioController(_mapperFake, _mediatorFake);
            var portfolio = new Portfolio { PortfolioId = 1 };
            A.CallTo(() => _mediatorFake.Send(A<PortfolioByIdQuery>._, A<CancellationToken>._)).Returns(portfolio);

            //Act
            await sut.Detail(1);

            //Assert
            A.CallTo(() => _mapperFake.Map<Portfolio, PortfolioViewModel>(portfolio)).MustHaveHappened();
        }

        [Fact]
        public async Task Detail_ModelNotNull_ReturnsCorrectView()
        {
            //Arrange
            var sut = new PortfolioController(_mapperFake, _mediatorFake);

            //Act
            var result = (ViewResult)await sut.Detail(1);

            //Assert
            result.Should().NotBeNull();
            result.Should().BeOfType<ViewResult>();
            result.ViewName.Should().BeNull();
        }

        [Fact]
        public async Task Detail_ModelNotNull_ReturnsCorrectPortfolioViewModel()
        {
            //Arrange
            var sut = new PortfolioController(_mapperFake, _mediatorFake);
            var portfolio = new Portfolio { PortfolioId = 1 };
            var portfolioViewModel = new PortfolioViewModel { PortfolioId = 1 };

            A.CallTo(() => _mediatorFake.Send(A<PortfolioByIdQuery>._, A<CancellationToken>._)).Returns(portfolio);
            A.CallTo(() => _mapperFake.Map<Portfolio, PortfolioViewModel>(A<Portfolio>._)).Returns(portfolioViewModel);

            //Act
            var result = (ViewResult)await sut.Detail(1);

            //Assert

            result.Model.Should().NotBeNull();
            result.Model.Should().BeOfType<PortfolioViewModel>();
            result.Model.Should().Be(portfolioViewModel);
        }

        [Fact]
        public async Task Index_Always_ReturnsTheCorrectView()
        {
            //Arrange
            var sut = new PortfolioController(_mapperFake, _mediatorFake);

            var portfolios = new[]
            {
                new Portfolio {PortfolioId = 1, PortfolioCategory = "MVC, BS"},
                new Portfolio {PortfolioId = 2, PortfolioCategory = "MVC, BS"}
            }.AsQueryable();

            //Act
            var result = (ViewResult)await sut.Index();

            //Assert
            result.Should().NotBeNull();
            result.Should().BeOfType<ViewResult>();
            result.ViewName.Should().BeNull();
        }

        [Fact]
        public async Task Index_WhenCalled_PassCorrectPortfoliosToMapMethod()
        {
            //Arrange
            var sut = new PortfolioController(_mapperFake, _mediatorFake);

            var portfolios = new[]
            {
                new Portfolio {PortfolioId = 1, PortfolioCategory = "MVC, BS"},
                new Portfolio {PortfolioId = 2, PortfolioCategory = "MVC, BS"}
            }.AsQueryable();

            A.CallTo(() => _mediatorFake.Send(A<GetAllPortfoliosQuery>._, A<CancellationToken>._)).Returns(portfolios);

            //Act
            await sut.Index();

            //Assert
            A.CallTo(()=> _mapperFake.Map<IQueryable<Portfolio>, List<PortfolioViewModel>>(A<IQueryable<Portfolio>>.That.Matches(p=> p== portfolios))).MustHaveHappened();
        }

        [Fact]
        public async Task Index_WhenCalled_PassCorrectArgumentsToPopulatePortfolioCategoryListCommand()
        {
            //Arrange
            var sut = new PortfolioController(_mapperFake, _mediatorFake);

            var portfolios = new[]
            {
                new Portfolio {PortfolioId = 1, PortfolioCategory = "MVC, BS"},
                new Portfolio {PortfolioId = 2, PortfolioCategory = "MVC, BS"}
            }.AsQueryable();

            var viewModel = new List<PortfolioViewModel>
            {
                new PortfolioViewModel {PortfolioId = 1, PortfolioCategory = "MVC, BS"},
                new PortfolioViewModel {PortfolioId = 2, PortfolioCategory = "MVC, BS"}
            };

            A.CallTo(() => _mediatorFake.Send(A<GetAllPortfoliosQuery>._, A<CancellationToken>._)).Returns(portfolios);
            A.CallTo(() => _mapperFake.Map<IQueryable<Portfolio>, List<PortfolioViewModel>>(A<IQueryable<Portfolio>>._)).Returns(viewModel);

            //Act
            var result = (ViewResult)await sut.Index();

            //Assert
            A.CallTo(() => _mediatorFake.Send(
                    A<PopulatePortfolioCategoryListCommand>.That
                        .Matches(p => p.Portfolios == portfolios && p.ViewModel == viewModel), A<CancellationToken>._))
                .MustHaveHappened();
        }

        [Fact]
        public async Task Index_WhenCalled_ReturnsCorrectViewModel()
        {
            //Arrange
            var sut = new PortfolioController(_mapperFake, _mediatorFake);

            var viewModel = new List<PortfolioViewModel>
            {
                new PortfolioViewModel {PortfolioId = 1, PortfolioCategory = "MVC, BS"},
                new PortfolioViewModel {PortfolioId = 2, PortfolioCategory = "MVC, BS"}
            };

            A.CallTo(() => _mapperFake.Map<IQueryable<Portfolio>, List<PortfolioViewModel>>(A<IQueryable<Portfolio>>._)).Returns(viewModel);

            //Act
            var result = (ViewResult)await sut.Index();

            //Assert
            result.Model.Should().Be(viewModel);
        }
    }
}