const express = require('express');
const process = require('process');
const path = require('path');
const cors = require('cors');
const notificationServices = require('./api/routes/notificationServices');
const users = require('./api/routes/users');
const history = require('./api/routes/history');

const PORT = process.env.PORT || 9005;

const app=express();

// Middleware configuration
app.use(express.json());
app.use(express.urlencoded({ extended: true }));
app.use(cors());

// View engine setup
app.set('view engine', 'pug');
app.set('views', path.join(__dirname, 'views'));

app.use('/', notificationServices);
app.use('/', users);
app.use('/', history);

// 404 error handler
app.use((req, res, next) => {
  const err = new Error('This page was not found!');
  err.status = 404;
  next(err);
});

// Global error handler
app.use((err, req, res, next) => {
 res.locals.message = err.message;
 res.locals.error = req.app.get('env') === 'development' ? err : {};

 res.status(err.status || 500);
 res.render('error');
});

// Start the server
app.listen(PORT, async () => {
  console.log(`Listening on ${PORT}...`);
});

module.exports = app;