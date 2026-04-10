const UsersModel = require('../models/UsersModel');
const ini = require('ini');
const fs = require('fs');
const path = require('path');
const nodemailer = require('nodemailer');
const handlebars = require('handlebars');

const config = ini.parse(fs.readFileSync('../config.ini','utf-8'));

const createTransporter = async () => {
    const transporter = nodemailer.createTransport({
        service: 'gmail',
        auth: {
            user: config.email.mailSender,
            pass: config.email.mailSenderPassword,
        },
        tls: {
            rejectUnauthorized: false, // Only use in development; handle properly in prod
        },
    });

    try {
        await transporter.verify();
        console.log('SMTP server is ready to send emails.');
    } catch (error) {
        console.error('SMTP verification failed:', error.message);
        throw new Error('Email transporter verification failed.');
    }

    return transporter;
};

const registerHandlebarsHelpers = () => {
    handlebars.registerHelper('or', (a, b) => a || b);
    handlebars.registerHelper('eq', (a, b) => a === b);
};

registerHandlebarsHelpers();

const compileTemplate = (templatePath, data) => {
    const templateSource = fs.readFileSync(templatePath, 'utf8');
    const template = handlebars.compile(templateSource);
    return template(data);
};

const sendMail = async (req) => {
    try {
        const { receiverId, dataToSend } = req.body;

        const receiver = await UsersModel.UsersModel.findOne({
            where: { id: parseInt(receiverId, 10) }
        });

        if (!receiver) {
            throw new Error('User not found with the given ID.');
        }
        const transporter = await createTransporter();

        const templatePath = path.resolve(__dirname, '../../templates/template.hbs');
        const htmlToSend = compileTemplate(templatePath, {
            arrayOlx: dataToSend.olxData,
            arrayPepper: dataToSend.pepper_data,
            arrayAmazon: dataToSend.amazon_data,
            arrayAllegro: dataToSend.allegro_data,
            arrayOtoMoto: dataToSend.otoMotoData,
            arrayOtoDom: dataToSend.otoDomData,
            arraySprzedajemy: dataToSend.sprzedajemyData,
        });
       // console.log(htmlToSend)
        const mailOptions = {
            from: config.email.mailSender,
            to: receiver.email,
            subject: 'Oferty sprzedażowe - mailing!',
            html: htmlToSend,
        };
///dorobić że jak puste dane to neio wysyłąj maila
        const result = await transporter.sendMail(mailOptions);
        console.log('Email sent successfully:', result.response);
    } catch (error) {
        console.error('Failed to send email:', error.message);
    }
};

module.exports = { sendMail };