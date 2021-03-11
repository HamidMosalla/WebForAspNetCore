using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FreelancerBlog.Core.DomainModels;
using FreelancerBlog.Core.Queries.Data.Articles;
using FreelancerBlog.Data.EntityFramework;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FreelancerBlog.Data.Queries.Articles
{
    public class GetAriclesQueryHandler : RequestHandler<GetArticlesQuery, IQueryable<Article>>
    {
        private FreelancerBlogContext _context;

        public GetAriclesQueryHandler(FreelancerBlogContext context)
        {
            _context = context;
        }

        protected override IQueryable<Article> Handle(GetArticlesQuery message)
        {
            return _context.Articles.Include(a => a.ApplicationUser).Include(a => a.ArticleRatings).Include(a => a.ArticleComments);
        }
    }
}
