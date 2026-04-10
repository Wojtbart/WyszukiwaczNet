const smsDiscordService = require('../servicelayer/smsDiscord_service');

const sendSmsToPhoneController = async (req, res) => {
    try {
        await smsDiscordService.sendSms(req);

        return res.status(201).json({
            success: true,
            message: 'SMS sent successfully!',
        });
    } catch (error) {
        console.error('Error sending SMS:', error);
        return res.status(500).json({
            success: false,
            message: 'An error occurred while sending the SMS.',
            error: error.message,
        });
    }
};

const sendMessageToDiscordController = async (req, res) => {
    try {
        await smsDiscordService.sendMessageToDiscord(req); 

        return res.status(201).json({
            success: true,
            message: 'Message sent to Discord successfully!',
        });
    } catch (error) {
        console.error('Error sending message to Discord:', error);
        return res.status(500).json({
            success: false,
            message: 'An error occurred while sending the message to Discord.',
            error: error.message,
        });
    }
};

module.exports = {
    sendSmsToPhoneController,
    sendMessageToDiscordController,
}