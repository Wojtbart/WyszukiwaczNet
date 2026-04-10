const ini = require('ini');
const fs = require('fs');
const Sequelize = require("sequelize");

const config = ini.parse(fs.readFileSync('../config.ini','utf-8'));


// const sequelize = new Sequelize(
//   config.mysql.database,
//   config.mysql.user,
//   config.mysql.password,
//   {
//     host: config.mysql.host,
//     host: 'localhost',
//     dialect: config.mysql_dialect.dialect,
//     dialectModule: require('mysql2'),
//     operationsAliases: false
//   }
// );

const sequelize = new Sequelize(
  config.postgres.database,    // PostgreSQL database name
  config.postgres.user,        // PostgreSQL user
  config.postgres.password,    // PostgreSQL password
  {
    host: config.postgres.host, // PostgreSQL host
    dialect: 'postgres',        // Use PostgreSQL dialect
    logging: false,             // Disable SQL logging (optional)
    port: config.postgres.port || 5432, // Default PostgreSQL port
  }
);

// sequelize.authenticate().then(() => {
//   console.log('Nawiązano pomyślnie  połączenie do bazy MySQL.');
// }).catch((error) => {
//   console.error('Nie można połączyć się z bazą danych: ', error);
// });
// sequelize.sync();

// Test the database connection
sequelize.authenticate()
  .then(() => console.log('✅ Połączono z bazą PostgreSQL!'))
  .catch(error => console.error('❌ Błąd połączenia z PostgreSQL:', error));

sequelize.sync(); // Sync database (ensure models are loaded before calling this)

module.exports={sequelize};