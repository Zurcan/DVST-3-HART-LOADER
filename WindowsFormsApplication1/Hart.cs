//using Form1;
using System;
using System.Data;

public class HartProtocol

{
    //public HartProtocol()
    //{
        
    //}
    // количество байт данных в сообщении не должно превышать 25
    public static int WaitingBytesQ = 8, ManufacturerID, DevTypeCode, PVunitsCode, UniversalComRev, DevSpecComRev, SoftwareRev, HardwareRev, DevFuncFlags, DevIDNumber, DevSerialNumber;
    public static int NumberOfPreambulas_send = 4, RecievedCommand, NumberOfPreambulas=4;
    public static int BurstModeEnabled = 0;
    public static int SlaveAddress = 0x02;
    public static int MasterAddress = 0x80;
    public static int LastSendedCommand = 0;
    public static int lastCommand = 0;
    public static byte[] SensorCurrentValue = new byte[4];
    public static byte[] SensorDiapPrcnt = new byte[4];
    public static byte[] PV = new byte[4];
    public static byte[] Tag = new byte[8];
    public static byte[] Descriptor = new byte[16];
    public static byte[] Message = new byte[32];
    public static byte[] Date = new byte[3];
    public static byte[] MeasuredCurrentZero = new byte[4];
    public static byte[] MeasuredCurrentGain = new byte[4];
    public static byte[] UpperRangeLimit = new byte[4];
    public static byte[] LowerRangeLimit = new byte[4];
    public static int[] CommandDataAnswerBytes = new int[34]   { 12, 5, 8, 9, 1, 12, 24, 21, 16, 17,  3, 24, 21,  3,  9,  0,  0,  0,  4,  0,  0,  0,  1,  4,  4,  1,  25, 3,  1,   1,   1,   1, 1, 1};
    private static int[] CommandDataRequestBytes = new int[34] { 0,  0, 0, 0, 1,  6,  0,  0,  0,  0,  0, 24, 21,  3,  9,  0,  0,  0,  4,  0,  5,  0,  1,  4,  4,  1,  0,  3,  1,   1,   1,   0, 32, 1};
    private static int[] CommandNumbersArray = new int[34]     { 0,  1, 2, 3, 6, 11, 12, 13, 14, 15, 16, 17, 18, 19, 35, 36, 37, 38, 40, 41, 42, 43, 44, 45, 46, 47, 48, 49, 59, 108, 109, 111, 0xAA, 0xAB};
    public static byte[] GenerateRequest(int commNumber)
    {
        byte[] messageToSend = new byte[HartProtocol.NumberOfPreambulas];
        for (int i = 0; i < NumberOfPreambulas;i++ ) messageToSend[i] = 0xff;
        Array.Resize(ref messageToSend, messageToSend.Length + 4 + CommandDataRequestBytes[commNumber]);
        if (HartProtocol.BurstModeEnabled == 1) { }
        else messageToSend[messageToSend.Length - 4 - CommandDataRequestBytes[commNumber]] = 0x02;//формируем стартовый символ, в данном случае он шлется от главного (0х02 - от главного, 0х06 - от подчиненного, 0х01 - пакетный режим)
        messageToSend[messageToSend.Length - 3 - CommandDataRequestBytes[commNumber]] = (byte)(MasterAddress | SlaveAddress | BurstModeEnabled);//формируем байт адреса, он составляется из адреса главного устройства(старший бит), адреса подчиненного устройства (4 младших бита) и флага пакетного режима (6й бит)
        messageToSend[messageToSend.Length - 2 - CommandDataRequestBytes[commNumber]] = (byte)CommandNumbersArray[commNumber];//формируем байт номера команды, который находится в соответствующем массиве
        messageToSend[messageToSend.Length - 1 - CommandDataRequestBytes[commNumber]] = (byte)CommandDataRequestBytes[commNumber];//формируем байт числа, который находится в соответствующем массиве
        if (CommandDataRequestBytes[commNumber]!=0)
        {
            byte[] buf = ReadInputdataToSend(commNumber); 
            for (int i = 0; i < buf.Length;i++ )
                messageToSend[messageToSend.Length - buf.Length + i] = buf[i];
        }

        return AppendCRC(messageToSend);
        
    }
    public static void GenerateAnswer(byte[] ReadBuff_)
    {
        int commNumber=0;
        //Array.Resize(ref ReadBuff, ReadBuff.Length - NumberOfPreambulas);
        //byte[] ReadBuff_=CutOffPreambulasRecieved(ReadBuff);
        //NumberOfPreambulas=0;
        if (CheckCRC(ReadBuff_) == 1) 
        {
            if (ReadBuff_[0] == 0x06)//проверяем стартовый символ, в данном случае он шлется от подчиненного (0х02 - от главного, 0х06 - от подчиненного, 0х01 - пакетный режим)
            {
                if (ReadBuff_[1 ] >= 0x80)//проверяем корректность адреса подчиненного и обращение к ПК 
                {
                    for (int a=0; a < 32;a++ )                    
                        if (CommandNumbersArray[a] == ReadBuff_[2]) commNumber = a;
                    
                    if (ReadBuff_[3] == CommandDataAnswerBytes[commNumber] + 2)//проверяем корректность принятого числа байт
                    {
                        RecievedCommand = ReadBuff_[2 ];
                        if (RecievedCommand == 0)//нулевая команда
                        {
                            ManufacturerID = ReadBuff_[7 ];
                            DevTypeCode = ReadBuff_[8 ];
                            NumberOfPreambulas = ReadBuff_[9 ];
                            UniversalComRev = ReadBuff_[10 ];
                            DevSpecComRev = ReadBuff_[11 ];
                            SoftwareRev = ReadBuff_[12 ];
                            HardwareRev = ReadBuff_[13 ];
                            DevFuncFlags = ReadBuff_[14 ];
                            DevIDNumber = (int)ReadBuff_[15 ] << 16 | (int)ReadBuff_[16 ] << 8 | (int)ReadBuff_[17 ];
                            //FF FF FF FF 06 82 00 0E 00 00 00 56 03 03 01 01 01 21 00 14 06 0B E5
                        }
                        if (RecievedCommand == 1)
                        {
                            PVunitsCode= ReadBuff_[6];
                            for (int i = 0; i < 4; i++)
                            {
                                
                                PV[i] = ReadBuff_[7 + i ];
                            }
                        }
                        if (RecievedCommand == 2)
                        {
                            for (int i = 0; i < 4; i++)
                            {
                                SensorCurrentValue[i] = ReadBuff_[6 + i ];
                                SensorDiapPrcnt[i] = ReadBuff_[10 + i ];
                            }
                        }
                        if (RecievedCommand == 16)
                        {
                            DevSerialNumber = (int)ReadBuff_[6] << 16 | (int)ReadBuff_[7] << 8 | (int)ReadBuff_[8];
                        }
                    }
                    else { }//ошибка формирования фрейма

                }
                else { }//address error
            }
            if (ReadBuff_[0] == 0x01)//пакетный режим
            {

            }
            else//ошибка адреса
            {

            }
        }
        
    }
    public static byte[] CutOffPreambulasRecieved(byte[] rec_mes)
    {
        //int i=0;
        Array.Reverse(rec_mes);
        Array.Resize(ref rec_mes,rec_mes.Length-2);
        Array.Reverse(rec_mes);
        while(rec_mes[0]==0xFF)
        {
            Array.Reverse(rec_mes);
            Array.Resize(ref rec_mes, rec_mes.Length - 1);
            Array.Reverse(rec_mes);
        }
        return rec_mes;
    }
    public static byte[] ReadInputdataToSend(int commNumber)
    {
        byte[] dataToSend = new byte[CommandDataAnswerBytes[commNumber]];
        
        
            if (commNumber == 4) 
            {
                dataToSend[0] = (byte)SlaveAddress;
            }
            
            if (commNumber== 5)
            {
                dataToSend = HartProtocol.ConvertASCIIToHartASCII(Tag);
            }

            if (commNumber==11)
            {
                dataToSend = HartProtocol.ConvertASCIIToHartASCII(Message);
            }

            if (commNumber == 12)
            {
                byte[] tmp_tag = HartProtocol.ConvertASCIIToHartASCII(Tag);
                byte[] tmp_desc = HartProtocol.ConvertASCIIToHartASCII(Descriptor);
                for (int i = 0; i < tmp_tag.Length;i++ )
                {
                    dataToSend[i] = tmp_tag[i];
                }
                for (int i = tmp_tag.Length; i < tmp_tag.Length+tmp_desc.Length; i++)
                {
                    dataToSend[i] = tmp_desc[i-tmp_tag.Length];
                }
                for (int i = tmp_tag.Length + tmp_desc.Length; i < tmp_tag.Length + tmp_desc.Length + Date.Length;i++ )
                {
                    dataToSend[i] = Date[i - tmp_tag.Length - tmp_desc.Length];
                }
            }

            if (commNumber == 14)
            {
                dataToSend[0] = (byte)HartProtocol.PVunitsCode;
                for (int i = 1; i < 5; i++)
                {
                    dataToSend[i] = HartProtocol.UpperRangeLimit[i-1];
                    dataToSend[i + 4] = HartProtocol.LowerRangeLimit[i - 1];
                }
            }
            if (commNumber == 23)
            {
                dataToSend= HartProtocol.MeasuredCurrentZero;
            }
            if (commNumber == 24)
            {
                dataToSend = HartProtocol.MeasuredCurrentGain;
            }

        return dataToSend;
    }

    

    public static byte[] ConvertHartASCIIToASCII(byte[] DataToConvert)
    {
        Int32 tmp;
        byte[] tmp_b = new byte[DataToConvert.Length*4/3];
        byte[] tmptmp_b = new byte[DataToConvert.Length];
        for (int i = 0; i < DataToConvert.Length/3; i++)
        {
            
            tmp = (Int32)DataToConvert[3*i] << (16) | (Int32)DataToConvert[3*i + 1] << (8) |  (Int32)DataToConvert[3*i + 2];
            tmp_b[4*i] = (byte)(((tmp >> 18) & 0x0000003f)|((tmp >> 23)^1)<<6);
            tmp_b[4*i + 1] = (byte)(((tmp >> 12) & 0x0000003f) | ((tmp >> 23) ^ 1) << 6);
            tmp_b[4*i + 2] = (byte)(((tmp >> 6) & 0x0000003f) | ((tmp >> 23) ^ 1) << 6);
            tmp_b[4*i + 3] = (byte)((tmp & 0x0000003f) | ((tmp >> 23) ^ 1) << 6);
            //for (int j = 0; j < 4; j++)
            //{
            //    tmptmp_b[3 - j] = tmp_b[j];
            tmp = 0;      
            //}
        }
        return tmp_b;
    }
    public static byte[] ConvertASCIIToHartASCII(byte[] DataToConvert)
    {
        Int32 tmp;
        byte[] tmp_b = new byte[4];
        if (DataToConvert.Length % 4 !=0)
        {
            for (int i = 1; i <= DataToConvert.Length % 4;i++ )
            {
                Array.Resize(ref DataToConvert, DataToConvert.Length + 1);
            }
        }
        byte[] tmptmp_b = new byte[DataToConvert.Length];
        for (int i = 0; i < DataToConvert.Length / 4; i++)
        {
            tmp = (Int32)(0x3f & DataToConvert[4 * i]) << (18) | (Int32)(0x3f & DataToConvert[4 * i + 1]) << (12) | (Int32)(0x3f & DataToConvert[4 * i + 2]) << (6) | (Int32)(0x3f & DataToConvert[4 * i + 3]);
            tmp_b = BitConverter.GetBytes(tmp);
            for (int j = 0; j < 3; j++)
            {
                tmptmp_b[3*i+2-j] = tmp_b[j];
                tmp_b[j] = 0;
            }
            Array.Resize(ref tmptmp_b, tmptmp_b.Length - 1);
            tmp = 0;
       
        }
        return tmptmp_b;
    }
    public static byte[] AppendCRC(byte[] msg)
    {
        byte CRC=0x00;
        for (int i = HartProtocol.NumberOfPreambulas; i < msg.Length; ++i)
        {
            CRC ^= msg[i];
        }
        Array.Resize(ref msg, msg.Length + 1);
        msg[msg.Length-1] = CRC;
        return msg;
    }

    public static int CheckCRC(byte[] msg)
    {
        byte CRC = 0;
        for (int i = 0; i < msg.Length - 1; ++i)
        {
            CRC ^= msg[i];
        }
        if (CRC == msg[msg.Length - 1]) CRC = 1;
        else CRC = 0;
        return CRC;
    }
    public static bool CheckMessageIntegrity(byte[] recMes)
    {
        recMes = CutOffPreambulasRecieved(recMes);
        if (recMes[3] + 4 > recMes.Length) return false;
        else return true;

    }
    public static byte[] CutOffGhostBytes(byte[] recMes)
    {
        int lastRealByteIndex = 5 + recMes[3];
        Array.Resize(ref recMes, lastRealByteIndex);
        return recMes;
    }
    public static int GetCommandDataLength(int commNumber)
    {
        int bytesInAnswer = 0;
        for (int i = 0; i < CommandNumbersArray.Length; i++)
            if (CommandNumbersArray[i] == commNumber)
            {
                bytesInAnswer = 7 + CommandDataAnswerBytes[i];
                return bytesInAnswer;
            }

        return bytesInAnswer;
    }
    public static byte[] createMessageToSend(byte comNum, byte[] comData)// эта функция нужна, чтобы записать любое количество необходимых байт в формате харта
    {
        byte[] messageToSend = new byte[HartProtocol.NumberOfPreambulas];
        for (int i = 0; i < NumberOfPreambulas; i++) messageToSend[i] = 0xff;
        int tmpCommandLength = 0;
            for(int i =0; i<CommandNumbersArray.Length; i++)
                if(comNum==CommandNumbersArray[i])tmpCommandLength=CommandDataRequestBytes[i];
        if (tmpCommandLength == 0)
        {
            Array.Resize(ref messageToSend, messageToSend.Length + 4);
            if (HartProtocol.BurstModeEnabled == 1) { }
            else messageToSend[messageToSend.Length - 4] = 0x02;//формируем стартовый символ, в данном случае он шлется от главного (0х02 - от главного, 0х06 - от подчиненного, 0х01 - пакетный режим)
            messageToSend[messageToSend.Length - 3] = (byte)(MasterAddress | SlaveAddress | BurstModeEnabled);//формируем байт адреса, он составляется из адреса главного устройства(старший бит), адреса подчиненного устройства (4 младших бита) и флага пакетного режима (6й бит)
            messageToSend[messageToSend.Length-2] = comNum;//формируем байт номера команды, который находится в соответствующем массиве
            //messageToSend[messageToSend.Length - 1 - comData.Length] = (byte)comData.Length;//формируем байт числа, который находится в соответствующем массиве
        }
        else
        {
            Array.Resize(ref messageToSend, messageToSend.Length + 4 + comData.Length);
            if (HartProtocol.BurstModeEnabled == 1) { }
            else messageToSend[messageToSend.Length - 4 - comData.Length] = 0x02;//формируем стартовый символ, в данном случае он шлется от главного (0х02 - от главного, 0х06 - от подчиненного, 0х01 - пакетный режим)
            messageToSend[messageToSend.Length - 3 - comData.Length] = (byte)(MasterAddress | SlaveAddress | BurstModeEnabled);//формируем байт адреса, он составляется из адреса главного устройства(старший бит), адреса подчиненного устройства (4 младших бита) и флага пакетного режима (6й бит)
            messageToSend[messageToSend.Length - 2 - comData.Length] = comNum;//формируем байт номера команды, который находится в соответствующем массиве
            messageToSend[messageToSend.Length - 1 - comData.Length] = (byte)comData.Length;//формируем байт числа, который находится в соответствующем массиве
        }
        if ((comData.Length != 0)&(comNum!=0))
        {
            byte[] buf = comData;
            for (int i = 0; i < buf.Length; i++)
                messageToSend[messageToSend.Length - buf.Length + i] = buf[i];
        }

        return AppendCRC(messageToSend);
    }

}
