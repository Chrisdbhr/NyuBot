using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using MongoDB.Bson;
using MongoDB.Driver;
using NyuBot.Extensions;

namespace NyuBot {
	public class DatabaseService {

		public readonly MongoClient dbClient;
		
		public DatabaseService(IConfigurationRoot config) {
			this.dbClient = new MongoClient(config[@"db-connection-string"]);
			var database = this.dbClient.GetDatabase ("db0");
			this.InsertSampleData(database).CAwait();
		}

		private async Task InsertSampleData(IMongoDatabase db) {
			var collection = db.GetCollection<BsonDocument> ("collection0");
			var document = new BsonDocument { { "student_id", 20000 }, {
					"scores",
					new BsonArray {
						new BsonDocument { { "type", "exam" }, { "score", 88.12334193287023 } },
						new BsonDocument { { "type", "quiz" }, { "score", 74.92381029342834 } },
						new BsonDocument { { "type", "homework" }, { "score", 89.97929384290324 } },
						new BsonDocument { { "type", "homework" }, { "score", 82.12931030513218 } }
					}
				}, { "class_id", 1180 }
			};
			await collection.InsertOneAsync(document);
		}
		
	}
}
