const emailService = require('../servicelayer/email_service');

const sendMailController = async (req, res) => {
    try 
    {
        const { receiverId, dataToSend } = req.body;

        if (!receiverId  || !dataToSend) 
        {
            return res.status(400).json({
                success: false,
                message: 'Missing required fields: "receiverId", "dataToSend" are required.',
            });
        }

        await emailService.sendMailService(req);

        res.status(201).json({
            success: true,
            message: 'Email sent successfully!',
        });
    } 
    catch (err) 
    {
        console.error('Error sending email:', err);

        res.status(500).send({
            success: false,
            message: 'An error occurred while sending the email!',
            error: err.message
        });
    }
};

module.exports = { sendMailController };
