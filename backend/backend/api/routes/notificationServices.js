var express = require('express');
var router = express.Router();

const email = require('../controllers/emailController');
const smsDiscord = require('../controllers/smsDiscordController');
const getDataFromScripts = require('../controllers/getDataFromScriptsController');
const cron = require('../controllers/cron');

// Email route
router.post('/email', email.sendMailController);

// Discord and SMS routes
router.post('/discord', smsDiscord.sendMessageToDiscordController);
router.post('/sms', smsDiscord.sendSmsToPhoneController);

// Data and cron jobs routes
router.post('/getData', getDataFromScripts.getDataFromWebsite);
router.post('/cronJob', cron.sendNotificationJob);
router.post('/deleteJobsForUser', cron.deleteJobsForUser);

module.exports = router;