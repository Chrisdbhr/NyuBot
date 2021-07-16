using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using MongoDB.Bson;
using MongoDB.Driver;

namespace NyuBot {
	public class DatabaseService {

		private readonly MongoClient _dbClient;
		private readonly IMongoDatabase _database;
		
		
		public DatabaseService(IConfigurationRoot config) {
			this._dbClient = new MongoClient(config[@"db-connection-string"]);
			this._database = this._dbClient.GetDatabase ("db0");
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

		public async Task InsertData(string collectionName, BsonDocument data){
			var collection = this._database.GetCollection<BsonDocument> (collectionName);
			await collection.InsertOneAsync(data);
		}

		public async Task<BsonDocument> GetData(string collectionName, FilterDefinition<BsonDocument> filter) {
			var collection = this._database.GetCollection<BsonDocument> (collectionName);
			return await collection.Find(filter).FirstOrDefaultAsync();
		}

		public async Task<UpdateResult> UpdateData(string collectionName, FilterDefinition<BsonDocument> filter, UpdateDefinition<BsonDocument> update) {
			var collection = this._database.GetCollection<BsonDocument> (collectionName);
			return await collection.UpdateOneAsync(filter, update, new UpdateOptions {
				IsUpsert = true
			});
		}

		
	}
}
