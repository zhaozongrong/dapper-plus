using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Xunit;
using Dapper;
using MySql.Data.MySqlClient;

namespace DapperPlus.Test
{
    public class DapperPlusTest
    {
        private DapperPlus<MySqlConnection> dapper=new DapperPlus<MySqlConnection>("Server=127.0.0.1;Database=db_dapper;Uid=root;Pwd=xxx;");
        
       [Fact]
       public async Task InsertTest()
       {
           var sql = "INSERT INTO author (NickName,RealName) VALUES(@nickName,@RealName)";
           var affected = await dapper.ExecuteAsync(sql,
               new Author[] {new Author("Colin", "Colin Chang"), new Author("Robin", "Robin Song")});

           Assert.Equal(2, affected);
       }

        [Fact]
        public async Task UpdateTest()
        {
            string sql = "UPDATE author SET Address=@address WHERE Id=@id";
            var affected = await dapper.ExecuteAsync(sql, new {id = 1, address = "山东"});

            Assert.Equal(1, affected);
        }

        [Fact]
        public async Task DeleteTest()
        {
            string sql = "DELETE FROM author WHERE Id=@id";
            var affected = await dapper.ExecuteAsync(sql, new {id = 3});

            Assert.Equal(0, affected);
        }

        [Fact]
        public async Task QueryScalarTest()
        {
            string sql = "SELECT COUNT(1) FROM author WHERE Id=@id AND NickName=@nickName";
            var cnt = await dapper.QueryScalarAsync(sql, new {id = 1, nickName = "Colin"});

            Assert.Equal(1, Convert.ToInt32(cnt));
        }

        [Fact]
        public async Task SimpleQueryTest()
        {
            var sql = "SELECT * FROM author WHERE Id=@id";
            var authors = await dapper.QueryAsync<Author>(sql, new {id = 1});

            Assert.NotNull(authors.FirstOrDefault());
        }

        [Fact]
        public async Task InQueryTest()
        {
            var sql = "SELECT * FROM author WHERE Id IN @ids";
            var authors = await dapper.QueryAsync<Author>(sql, new {ids = new int[] {1, 2}});

            Assert.Equal(2, authors.Count());
        }

        [Fact]
        public async Task JoinQueryTest()
        {
            var sql = @"SELECT * FROM
                article AS ar
                JOIN author AS au ON ar.AuthorId = au.Id
                LEFT JOIN `comment` AS c ON ar.Id = c.ArticleId";
            var articles = new Dictionary<int, Article>();
            var data = await dapper.QueryAsync<Article, Author, Comment, Article>(sql,
                (article, author, comment) =>
                {
                    //1:1
                    article.Author = author;

                    //1:N
                    if (!articles.TryGetValue(article.Id, out Article articleEntry))
                    {
                        articleEntry = article;
                        articleEntry.Comments = new List<Comment> { };
                        articles.Add(article.Id, articleEntry);
                    }

                    articleEntry.Comments.Append(comment);
                    return articleEntry;
                });
            var result1 = data.Distinct();
            var result2 = articles.Values;

            Assert.Equal(result1.Count(), result2.Count());
        }

        [Fact]
        public async Task JsonMultiQueryTest()
        {
            string sql1 = "SELECT * FROM article WHERE Id=@id";
            string sql2 = "SELECT * FROM `comment` WHERE ArticleId=@articleId";
            var data = await dapper.QueryMultipleAsync(new string[] {sql1, sql2}, new {id = 1, articleId = 1});
            var article = data.ElementAt(0).ElementAt(0).ToString(); //Json

            Assert.NotEmpty(article);
        }

        [Fact]
        public async Task MultiQueryTest()
        {
            var sqls = new string[]
            {
                "SELECT * FROM article WHERE Id=@id",
                "SELECT * FROM `comment` WHERE ArticleId=@articleId"
            };
            (IEnumerable<Article> Articles, IEnumerable<Comment> Comments) =
                await dapper.QueryMultipleAsync<Article, Comment>(sqls, new {id = 1, articleId = 1});
            Article article = Articles.FirstOrDefault();

            Assert.NotNull(article);
            article.Comments = Comments;

            Assert.True(article.Comments.Any());
        }

        [Fact]
        public async Task TransactionTest()
        {
            int affected = await dapper.ExecuteTransactionAsync(new SqlScript[]
            {
                new SqlScript("UPDATE article SET UpdateTime=NOW() WHERE Id=@id", new {id = 2}),
                new SqlScript("UPDATE author SET BirthDate=NOW() WHERE Id=@id", new {id = 1})
            });

            Assert.Equal(2, affected);
        }
    }
}
